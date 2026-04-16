using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	/// <summary>
	/// Consumes <see cref="MeshRequiresUpload"/> chunks, writes their CPU vertex/index
	/// data into a Unity <see cref="Mesh"/> via the MeshDataArray API, registers the mesh
	/// with <see cref="EntitiesGraphicsSystem"/>, and wires up Entities Graphics rendering
	/// components on two dedicated render-only companion entities:
	///   SolidEntity  → sub-mesh 0, solid material
	///   FluidEntity  → sub-mesh 1, water material (only when fluid faces exist)
	///
	/// The chunk entity itself remains a pure data entity with no rendering components.
	/// This avoids LocalToWorld timing issues (transform system runs before PresentationGroup).
	/// </summary>
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class ChunkRenderUploadSystem : SystemBase
	{
		// ── vertex layout ─────────────────────────────────────────────────────
		private static readonly VertexAttributeDescriptor[] k_VertexLayout =
		{
			new(VertexAttribute.TexCoord7, VertexAttributeFormat.Float32, 4)
		};

		// Local-space chunk bounds (32³ voxels, origin at 0,0,0)
		private static readonly Bounds k_ChunkLocalBounds =
			new(new Vector3(16f, 16f, 16f), new Vector3(32f, 32f, 32f));

		private static RenderMeshDescription RenderDesc => new(ShadowCastingMode.TwoSided, true, MotionVectorGenerationMode.Camera, LayerMask.NameToLayer("Chunk"), staticShadowCaster:true, lightProbeUsage:LightProbeUsage.Off);

		// ── Entities Graphics state ────────────────────────────────────────────
		private EntitiesGraphicsSystem m_EGS;
		private BatchMaterialID        m_SolidMatID;
		private BatchMaterialID        m_FluidMatID;
		private bool                   m_MaterialsRegistered;

		private EntityQuery m_UploadQuery;

		protected override void OnCreate()
		{
			m_UploadQuery = SystemAPI.QueryBuilder()
				.WithAll<MeshRequiresUpload, ChunkMeshData, ChunkPositionComponent>()
				.Build();
		}

		protected override void OnUpdate()
		{
			if (m_UploadQuery.IsEmpty) return;

			// ── lazy-register materials once ──────────────────────────────────
			if (!m_MaterialsRegistered)
			{
				if (!SystemAPI.TryGetSingletonEntity<WorldBlockRegistrySingleton>(out Entity regEntity) ||
				    !EntityManager.HasComponent<ChunkMaterialComponent>(regEntity))
					return;

				m_EGS = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
				if (m_EGS == null) return;

				var matComp  = EntityManager.GetComponentObject<ChunkMaterialComponent>(regEntity);
				m_SolidMatID = m_EGS.RegisterMaterial(matComp.SolidMaterial);
				m_FluidMatID = m_EGS.RegisterMaterial(matComp.WaterMaterial);
				m_MaterialsRegistered = true;
			}

			var entities = m_UploadQuery.ToEntityArray(Allocator.Temp);
			var ecb      = new EntityCommandBuffer(Allocator.Temp);

			for (int i = 0; i < entities.Length; i++)
			{
				Entity entity = entities[i];

				// ── complete any in-flight job for this chunk ──────────────────
				if (EntityManager.HasComponent<ChunkActiveJob>(entity))
					EntityManager.GetComponentData<ChunkActiveJob>(entity).Handle.Complete();

				if (!EntityManager.HasComponent<ChunkMeshData>(entity)) continue;
				var meshData = EntityManager.GetComponentData<ChunkMeshData>(entity);

				int totalV      = meshData.CombinedVertices.Length;
				int totalI      = meshData.CombinedIndices.Length;
				int solidICount = meshData.SolidIndexCount;
				int fluidICount = totalI - solidICount;

				// ── world position for render entities ─────────────────────────
				var wPos   = EntityManager.GetComponentData<ChunkPositionComponent>(entity).WorldPosition;
				var wPosF3 = new float3(wPos.x, wPos.y, wPos.z);

				// ── retrieve or create ChunkManagedMesh ────────────────────────
				bool             isFirstUpload = !EntityManager.HasComponent<ChunkManagedMesh>(entity);
				ChunkManagedMesh managed;

				if (isFirstUpload)
				{
					managed = new ChunkManagedMesh { Mesh = new Mesh { name = "ChunkMesh" } };
					EntityManager.AddComponentObject(entity, managed);
				}
				else
				{
					managed = EntityManager.GetComponentObject<ChunkManagedMesh>(entity);
					if (managed.MeshBatchID.value != 0)
						m_EGS.UnregisterMesh(managed.MeshBatchID);
				}

				// ── write geometry into the Mesh via MeshDataArray ─────────────
				Mesh.MeshDataArray mda = Mesh.AllocateWritableMeshData(1);
				Mesh.MeshData      md  = mda[0];

				if (totalV > 0)
				{
					md.SetVertexBufferParams(totalV, k_VertexLayout);
					md.SetIndexBufferParams(totalI, IndexFormat.UInt32);

					NativeArray<Vertex> dstV = md.GetVertexData<Vertex>();
					NativeArray<Vertex>.Copy(meshData.CombinedVertices.AsArray(), dstV, totalV);

					NativeArray<int> dstI = md.GetIndexData<int>();
					NativeArray<int>.Copy(meshData.CombinedIndices.AsArray(), dstI, totalI);

					md.subMeshCount = 2;
					md.SetSubMesh(0,
						new SubMeshDescriptor(0, solidICount)
						{
							bounds = k_ChunkLocalBounds, vertexCount = totalV
						},
						MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
					md.SetSubMesh(1,
						new SubMeshDescriptor(solidICount, fluidICount)
						{
							bounds = k_ChunkLocalBounds, vertexCount = totalV
						},
						MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
				}
				else
				{
					md.SetVertexBufferParams(0, k_VertexLayout);
					md.SetIndexBufferParams(0, IndexFormat.UInt32);
					md.subMeshCount = 2;
					md.SetSubMesh(0, new SubMeshDescriptor(0, 0));
					md.SetSubMesh(1, new SubMeshDescriptor(0, 0));
				}

				Mesh.ApplyAndDisposeWritableMeshData(mda, managed.Mesh,
					MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
				managed.Mesh.bounds = k_ChunkLocalBounds;

				// ── register mesh ──────────────────────────────────────────────
				managed.MeshBatchID = m_EGS.RegisterMesh(managed.Mesh);

				// ── solid render entity (sub-mesh 0) ───────────────────────────
				var solidMMI = new MaterialMeshInfo(m_SolidMatID, managed.MeshBatchID);

				if (managed.SolidEntity == Entity.Null || !EntityManager.Exists(managed.SolidEntity))
				{
					Entity solidEnt = EntityManager.CreateEntity();
					EntityManager.SetName(solidEnt, "ChunkSolid");
					EntityManager.AddComponentData(solidEnt, LocalTransform.FromPosition(wPosF3));
					EntityManager.AddComponentData(solidEnt, new PerInstanceCullingTag());
					EntityManager.AddComponentData(solidEnt, new DepthSorted_Tag());
					RenderMeshUtility.AddComponents(solidEnt, EntityManager, RenderDesc, solidMMI);
					managed.SolidEntity = solidEnt;
				}
				else
				{
					EntityManager.SetComponentData(managed.SolidEntity, solidMMI);
					EntityManager.SetComponentData(managed.SolidEntity,
						new RenderBounds { Value = k_ChunkLocalBounds.ToAABB() });
				}

				// ── fluid render entity (sub-mesh 1) ───────────────────────────
				var fluidMMI = new MaterialMeshInfo(m_FluidMatID, managed.MeshBatchID, 1);

				if (fluidICount > 0)
				{
					if (managed.FluidEntity == Entity.Null || !EntityManager.Exists(managed.FluidEntity))
					{
						Entity fluidEnt = EntityManager.CreateEntity();
						EntityManager.SetName(fluidEnt, "ChunkFluid");
						EntityManager.AddComponentData(fluidEnt, LocalTransform.FromPosition(wPosF3));
						EntityManager.AddComponentData(fluidEnt, new PerInstanceCullingTag());
						EntityManager.AddComponentData(fluidEnt, new DepthSorted_Tag());
						RenderMeshUtility.AddComponents(fluidEnt, EntityManager, RenderDesc, fluidMMI);
						managed.FluidEntity = fluidEnt;
					}
					else
					{
						EntityManager.SetComponentData(managed.FluidEntity, fluidMMI);
						EntityManager.SetComponentData(managed.FluidEntity,
							new RenderBounds { Value = k_ChunkLocalBounds.ToAABB() });
					}
				}
				else if (managed.FluidEntity != Entity.Null && EntityManager.Exists(managed.FluidEntity))
				{
					EntityManager.DestroyEntity(managed.FluidEntity);
					managed.FluidEntity = Entity.Null;
				}

				// ── bookkeeping ────────────────────────────────────────────────
				ecb.RemoveComponent<MeshRequiresUpload>(entity);
				if (!EntityManager.HasComponent<HasMesh>(entity))
					ecb.AddComponent<HasMesh>(entity);
			}

			entities.Dispose();
			ecb.Playback(EntityManager);
			ecb.Dispose();
		}
	}

	internal static class BoundsExtensions
	{
		public static AABB ToAABB(this Bounds b) =>
			new() { Center = b.center, Extents = b.extents };
	}
}
