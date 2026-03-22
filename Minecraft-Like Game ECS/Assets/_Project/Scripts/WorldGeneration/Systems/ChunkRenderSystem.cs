using System.Collections.Generic;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class ChunkRenderSystem : SystemBase
	{
		private Material                 material;
		private Dictionary<Entity, Mesh> meshes;
		private RenderParams             renderParams;

		protected override void OnCreate()
		{
			meshes = new Dictionary<Entity, Mesh>();
		}

		protected override void OnDestroy()
		{
			foreach (Mesh m in meshes.Values) Object.Destroy(m);
		}

		protected override void OnUpdate()
		{
			if (material == null)
			{
				if (SystemAPI.TryGetSingletonEntity<WorldSettingsSingleton>(out Entity settingsEntity) &&
				    EntityManager.HasComponent<ChunkMaterialComponent>(settingsEntity))
				{
					var matComp = EntityManager.GetComponentData<ChunkMaterialComponent>(settingsEntity);
					material = matComp.Material;
					renderParams = new RenderParams(material)
					                {
						                renderingLayerMask   = RenderingLayerMask.defaultRenderingLayerMask,
						                rendererPriority     = 0,
						                motionVectorMode     = MotionVectorGenerationMode.Camera,
						                reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox,
						                shadowCastingMode    = ShadowCastingMode.On,
						                receiveShadows       = true,
						                lightProbeUsage      = LightProbeUsage.BlendProbes,
						                matProps             = new MaterialPropertyBlock()
					                };
				}
				else
				{
					return;
				}
			}

			var ecb = new EntityCommandBuffer(Allocator.Temp);

			foreach ((RefRO<ChunkMeshData> meshData, Entity entity) in SystemAPI.Query<RefRO<ChunkMeshData>>()
				         .WithAll<NeedsMeshSync>()
				         .WithEntityAccess())
			{
				if (!meshes.TryGetValue(entity, out Mesh unityMesh))
				{
					unityMesh      = new Mesh();
					meshes[entity] = unityMesh;
				}

				UpdateUnityMesh(unityMesh, meshData.ValueRO);
				ecb.RemoveComponent<NeedsMeshSync>(entity);
				ecb.AddComponent<HasRenderMesh>(entity);
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();

			foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in SystemAPI.Query<RefRO<ChunkPositionComponent>>()
				         .WithAll<HasRenderMesh, IsVisible, NeedsRender>().WithEntityAccess())
				if (meshes.TryGetValue(entity, out Mesh unityMesh) && unityMesh.vertexCount > 0)
				{
					Matrix4x4 matrix = Matrix4x4.Translate(new Vector3(pos.ValueRO.WorldPosition.x,
					                                                   pos.ValueRO.WorldPosition.y,
					                                                   pos.ValueRO.WorldPosition.z));

					renderParams.worldBounds = new Bounds(
					                                       new Vector3(pos.ValueRO.WorldPosition.x + 16f,
					                                                   pos.ValueRO.WorldPosition.y + 16f,
					                                                   pos.ValueRO.WorldPosition.z + 16f),
					                                       new Vector3(32, 32, 32)
					                                      );

					Graphics.RenderMesh(renderParams, unityMesh, 0, matrix);
					if (unityMesh.subMeshCount > 1) Graphics.RenderMesh(renderParams, unityMesh, 1, matrix);
					if (unityMesh.subMeshCount > 2) Graphics.RenderMesh(renderParams, unityMesh, 2, matrix);
				}

			CleanupDestroyed();
		}

		private void UpdateUnityMesh(Mesh mesh, in ChunkMeshData nativeData)
		{
			mesh.Clear();

			var solidVCount  = nativeData.SolidMesh.IsCreated ? nativeData.SolidMesh.Vertices.Length : 0;
			var transpVCount = nativeData.TransparentMesh.IsCreated ? nativeData.TransparentMesh.Vertices.Length : 0;
			var fluidVCount  = nativeData.FluidMesh.IsCreated ? nativeData.FluidMesh.Vertices.Length : 0;

			var vCount = solidVCount + transpVCount + fluidVCount;
			if (vCount == 0) return;

			var allVerts = new NativeArray<Vertex>(vCount, Allocator.TempJob);
			var vOffset  = 0;

			if (solidVCount > 0)
			{
				NativeArray<Vertex>.Copy(nativeData.SolidMesh.Vertices.AsArray(), 0, allVerts, vOffset, solidVCount);
				vOffset += solidVCount;
			}

			var sV = vOffset;
			if (transpVCount > 0)
			{
				NativeArray<Vertex>.Copy(nativeData.TransparentMesh.Vertices.AsArray(), 0, allVerts, vOffset, transpVCount);
				vOffset += transpVCount;
			}

			var tV = vOffset;
			if (fluidVCount > 0) NativeArray<Vertex>.Copy(nativeData.FluidMesh.Vertices.AsArray(), 0, allVerts, vOffset, fluidVCount);

			var layout = new[]
			             {
				             new VertexAttributeDescriptor(VertexAttribute.Position),
				             new VertexAttributeDescriptor(VertexAttribute.Normal),
				             new VertexAttributeDescriptor(VertexAttribute.Tangent, dimension: 4),
				             new VertexAttributeDescriptor(VertexAttribute.TexCoord0),
				             new VertexAttributeDescriptor(VertexAttribute.TexCoord1)
			             };

			mesh.SetVertexBufferParams(vCount, layout);
			mesh.SetVertexBufferData(allVerts, 0, 0, vCount);

			var solidICount  = nativeData.SolidMesh.IsCreated ? nativeData.SolidMesh.Triangles.Length : 0;
			var transpICount = nativeData.TransparentMesh.IsCreated ? nativeData.TransparentMesh.Triangles.Length : 0;
			var fluidICount  = nativeData.FluidMesh.IsCreated ? nativeData.FluidMesh.Triangles.Length : 0;

			var iCount = solidICount + transpICount + fluidICount;
			mesh.SetIndexBufferParams(iCount, IndexFormat.UInt32);

			var subMeshes = 0;
			var iOffset   = 0;

			if (solidICount > 0)
			{
				mesh.SetIndexBufferData(nativeData.SolidMesh.Triangles.AsArray(), 0, iOffset, solidICount);
				subMeshes++;
				iOffset += solidICount;
			}

			if (transpICount > 0)
			{
				NativeArray<int> offsetArray = OffsetTriangles(nativeData.TransparentMesh.Triangles.AsArray(), sV);
				mesh.SetIndexBufferData(offsetArray, 0, iOffset, offsetArray.Length);
				offsetArray.Dispose();
				subMeshes++;
				iOffset += transpICount;
			}

			if (fluidICount > 0)
			{
				NativeArray<int> offsetArray = OffsetTriangles(nativeData.FluidMesh.Triangles.AsArray(), tV);
				mesh.SetIndexBufferData(offsetArray, 0, iOffset, offsetArray.Length);
				offsetArray.Dispose();
				subMeshes++;
			}

			mesh.subMeshCount = subMeshes;

			var activeSub = 0;
			iOffset = 0;
			if (solidICount > 0)
			{
				mesh.SetSubMesh(activeSub++, new SubMeshDescriptor(iOffset, solidICount));
				iOffset += solidICount;
			}

			if (transpICount > 0)
			{
				mesh.SetSubMesh(activeSub++, new SubMeshDescriptor(iOffset, transpICount));
				iOffset += transpICount;
			}

			if (fluidICount > 0) mesh.SetSubMesh(activeSub, new SubMeshDescriptor(iOffset, fluidICount));

			allVerts.Dispose();
		}

		private NativeArray<int> OffsetTriangles(NativeArray<int> inds, int offset)
		{
			var arr                                      = new NativeArray<int>(inds.Length, Allocator.TempJob);
			for (var i = 0; i < inds.Length; i++) arr[i] = inds[i] + offset;
			return arr;
		}

		private void CleanupDestroyed()
		{
			var toRemove = new List<Entity>();
			foreach (Entity e in meshes.Keys)
				if (!EntityManager.Exists(e))
					toRemove.Add(e);
			foreach (Entity e in toRemove)
			{
				Object.Destroy(meshes[e]);
				meshes.Remove(e);
			}
		}
	}
}