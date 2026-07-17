using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	/// <summary>
	///     Consumes <see cref="MeshRequiresUpload" /> chunks, writes their CPU vertex/index
	///     data into a Unity <see cref="Mesh" /> via the MeshDataArray API, registers the mesh
	///     with <see cref="EntitiesGraphicsSystem" />, and wires up Entities Graphics rendering
	///     components on two dedicated render-only companion entities:
	///     SolidEntity  → sub-mesh 0, solid material
	///     FluidEntity  → sub-mesh 1, water material (only when fluid faces exist)
	///     The chunk entity itself remains a pure data entity with no rendering components.
	///     This avoids LocalToWorld timing issues (transform system runs before PresentationGroup).
	/// </summary>
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class ChunkRenderUploadSystem : SystemBase
	{
		// ── vertex layout ─────────────────────────────────────────────────────
		private static readonly VertexAttributeDescriptor[] vertexLayout =
		{
			new(VertexAttribute.TexCoord7, VertexAttributeFormat.Float32, 4)
		};

		// Local-space chunk bounds (32³ voxels, origin at 0,0,0)
		private static readonly Bounds chunkLocalBounds =
			new(new Vector3(16f, 16f, 16f), new Vector3(32f, 32f, 32f));

		// ── Entities Graphics state ────────────────────────────────────────────
		private EntitiesGraphicsSystem egs;
		private BatchMaterialID        fluidMatID;
		private BatchMaterialID        solidMatID;
		private bool                   materialsRegistered;

		private EntityQuery uploadQuery;

		private static RenderMeshDescription RenderDesc => new(ShadowCastingMode.TwoSided, true,
		                                                       MotionVectorGenerationMode.Camera,
		                                                       LayerMask.NameToLayer("Chunk"), staticShadowCaster: true,
		                                                       lightProbeUsage: LightProbeUsage.Off);

		private const MeshUpdateFlags MESH_UPDATE_FLAGS =
			MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds |
			MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices |
			MeshUpdateFlags.DontValidateLodRanges;
		protected override void OnCreate()
		{
			uploadQuery = SystemAPI.QueryBuilder()
			                         .WithAll<MeshRequiresUpload, ChunkMeshData, ChunkPositionComponent>()
			                         .Build();
		}

		protected override void OnUpdate()
		{
			if (uploadQuery.IsEmpty) return;

			// ── lazy-register materials once ──────────────────────────────────
			if (!materialsRegistered)
			{
				if (!SystemAPI.TryGetSingletonEntity<WorldBlockRegistrySingleton>(out Entity regEntity) ||
				    !EntityManager.HasComponent<ChunkMaterialComponent>(regEntity))
					return;

				egs = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
				if (egs == null) return;

				var matComp = EntityManager.GetComponentData<ChunkMaterialComponent>(regEntity);
				solidMatID          = egs.RegisterMaterial(matComp.SolidMaterial.Value);
				fluidMatID          = egs.RegisterMaterial(matComp.WaterMaterial.Value);
				materialsRegistered = true;
			}

			// committed occlusion result (front tree) — never written by in-flight jobs,
			// so it's safe to read here; Valid is false until the first pass completes
			var occValid = SystemAPI.TryGetSingleton(out ChunkOcclusionTreeSingleton occState) && occState.Valid;

			NativeArray<Entity> entities = uploadQuery.ToEntityArray(Allocator.Temp);
			var                 ecb      = new EntityCommandBuffer(Allocator.Temp);

			for (var i = 0; i < entities.Length; i++)
			{
				Entity entity = entities[i];

				// ── don't stall on the mesh job ────────────────────────────────
				// If the mesh job for this chunk is still running, leave MeshRequiresUpload
				// on it and pick it up a later frame. Force-completing a running job here is
				// a main-thread block — the upload-time hitch. Completing one that already
				// reports IsCompleted just releases the fence and doesn't stall.
				if (EntityManager.HasComponent<ChunkActiveJob>(entity))
				{
					JobHandle h = EntityManager.GetComponentData<ChunkActiveJob>(entity).Handle;
					if (!h.IsCompleted) continue;
					h.Complete();
				}

				if (!EntityManager.HasComponent<ChunkMeshData>(entity)) continue;
				var meshData = EntityManager.GetComponentData<ChunkMeshData>(entity);

				var totalV      = meshData.CombinedVertices.Length;
				var totalI      = meshData.CombinedIndices.Length;
				var solidICount = meshData.SolidIndexCount;
				var fluidICount = totalI - solidICount;

				// ── world position for render entities ─────────────────────────
				var  posComp = EntityManager.GetComponentData<ChunkPositionComponent>(entity);
				int3 wPos    = posComp.WorldPosition;
				var  wPosF3  = new float3(wPos.x, wPos.y, wPos.z);

				// ── retrieve or create ChunkManagedMesh ────────────────────────
				var              isFirstUpload = !EntityManager.HasComponent<ChunkManagedMesh>(entity);
				ChunkManagedMesh managed;

				if (isFirstUpload)
				{
					managed = new ChunkManagedMesh { Mesh = new Mesh { name = "ChunkMesh" } };
					EntityManager.AddComponentData(entity, managed);
				}
				else
				{
					managed = EntityManager.GetComponentData<ChunkManagedMesh>(entity);
					if (managed.MeshBatchID.value != 0)
						egs.UnregisterMesh(managed.MeshBatchID);
				}

				// ── write geometry into the Mesh via MeshDataArray ─────────────
				Mesh.MeshDataArray mda = Mesh.AllocateWritableMeshData(1);
				Mesh.MeshData      md  = mda[0];

				if (totalV > 0)
				{
					md.SetVertexBufferParams(totalV, vertexLayout);
					md.SetIndexBufferParams(totalI, IndexFormat.UInt32);

					NativeArray<Vertex> dstV = md.GetVertexData<Vertex>();
					NativeArray<Vertex>.Copy(meshData.CombinedVertices.AsArray(), dstV, totalV);

					NativeArray<int> dstI = md.GetIndexData<int>();
					NativeArray<int>.Copy(meshData.CombinedIndices.AsArray(), dstI, totalI);

					md.subMeshCount = 2;
					md.SetSubMesh(0,
					              new SubMeshDescriptor(0, solidICount)
					              {
						              bounds = chunkLocalBounds, vertexCount = totalV
					              },
					              MESH_UPDATE_FLAGS);
					md.SetSubMesh(1,
					              new SubMeshDescriptor(solidICount, fluidICount)
					              {
						              bounds = chunkLocalBounds, vertexCount = totalV
					              },
					              MESH_UPDATE_FLAGS);
				}
				else
				{
					md.SetVertexBufferParams(0, vertexLayout);
					md.SetIndexBufferParams(0, IndexFormat.UInt32);
					md.subMeshCount = 2;
					md.SetSubMesh(0, new SubMeshDescriptor(0, 0));
					md.SetSubMesh(1, new SubMeshDescriptor(0, 0));
				}

				Mesh.ApplyAndDisposeWritableMeshData(mda, managed.Mesh.Value, MESH_UPDATE_FLAGS);
				managed.Mesh.Value.bounds = chunkLocalBounds;

				// ── register mesh ──────────────────────────────────────────────
				managed.MeshBatchID = egs.RegisterMesh(managed.Mesh.Value);

				// ── solid render entity (sub-mesh 0) ───────────────────────────
				var solidMMI = new MaterialMeshInfo(solidMatID, managed.MeshBatchID);

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
					                               new RenderBounds { Value = chunkLocalBounds.ToAABB() });
				}

				// ── fluid render entity (sub-mesh 1) ───────────────────────────
				var fluidMMI = new MaterialMeshInfo(fluidMatID, managed.MeshBatchID, 1);

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
						                               new RenderBounds { Value = chunkLocalBounds.ToAABB() });
					}
				}
				else if (managed.FluidEntity != Entity.Null && EntityManager.Exists(managed.FluidEntity))
				{
					EntityManager.DestroyEntity(managed.FluidEntity);
					managed.FluidEntity = Entity.Null;
				}

				// ── bookkeeping ────────────────────────────────────────────────
				// struct component: persist MeshBatchID / SolidEntity / FluidEntity
				EntityManager.SetComponentData(entity, managed);

				// Enforce the committed occlusion result on every (re)upload: a chunk
				// meshed inside a hidden region spawns hidden instead of flashing visible
				// until the next occlusion pass. The diff reconcile only touches chunks
				// whose visibility CHANGED between passes, so it relies on uploads leaving
				// entities consistent with the current front tree.
				var visible = !occValid || occState.Front.TestWorld(posComp.ChunkCoord);
				ApplyOcclusion(managed.SolidEntity, visible);
				ApplyOcclusion(managed.FluidEntity, visible);

				ecb.RemoveComponent<MeshRequiresUpload>(entity);
				if (!EntityManager.HasComponent<HasMesh>(entity))
					ecb.AddComponent<HasMesh>(entity);
			}

			entities.Dispose();
			ecb.Playback(EntityManager);
			ecb.Dispose();
		}

		private void ApplyOcclusion(Entity renderEntity, bool visible)
		{
			if (renderEntity == Entity.Null || !EntityManager.Exists(renderEntity)) return;

			var hidden = EntityManager.HasComponent<DisableRendering>(renderEntity);
			if (visible && hidden) EntityManager.RemoveComponent<DisableRendering>(renderEntity);
			else if (!visible && !hidden) EntityManager.AddComponent<DisableRendering>(renderEntity);
		}
	}

	internal static class BoundsExtensions
	{
		public static AABB ToAABB(this Bounds b)
		{
			return new AABB { Center = b.center, Extents = b.extents };
		}
	}
}