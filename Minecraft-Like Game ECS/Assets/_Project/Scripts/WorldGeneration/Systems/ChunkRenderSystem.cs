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
		private Material     material;
		private RenderParams renderParams;
		
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
#if !UNITY_EDITOR
						               camera = Camera.main,
#endif 
						               motionVectorMode     = MotionVectorGenerationMode.Camera,
						               reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox,
						               shadowCastingMode    = ShadowCastingMode.On,
						               receiveShadows       = true,
						               lightProbeUsage      = LightProbeUsage.BlendProbes,
						               matProps             = new MaterialPropertyBlock()
					               };
				}
				else return;
			}
			
			foreach ((ChunkMeshData meshData, RefRO<ChunkPositionComponent> pos, _) in SystemAPI.Query<ChunkMeshData, RefRO<ChunkPositionComponent>>()
				         .WithAll<HasMesh, IsVisible, NeedsRender>().WithEntityAccess())
			{
				if (meshData.ChunkMesh == null || meshData.ChunkMesh.vertexCount <= 0) continue;
				Matrix4x4 matrix = Matrix4x4.Translate(new Vector3(pos.ValueRO.WorldPosition.x,
				                                                   pos.ValueRO.WorldPosition.y,
				                                                   pos.ValueRO.WorldPosition.z));

				renderParams.worldBounds = new Bounds(
				                                      new Vector3(pos.ValueRO.WorldPosition.x + 16f,
				                                                  pos.ValueRO.WorldPosition.y + 16f,
				                                                  pos.ValueRO.WorldPosition.z + 16f),
				                                      new Vector3(32, 32, 32)
				                                     );

				Graphics.RenderMesh(renderParams, meshData.ChunkMesh, 0, matrix);
				if (meshData.ChunkMesh.subMeshCount > 1) Graphics.RenderMesh(renderParams, meshData.ChunkMesh, 1, matrix);
			}
		}
	}
}