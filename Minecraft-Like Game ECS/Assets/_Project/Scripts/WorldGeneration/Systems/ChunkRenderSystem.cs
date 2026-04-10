using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
	public partial class ChunkRenderSystem : SystemBase
	{
		private Material solidMaterial;
		private Material fluidMaterial;

		private RenderParams renderParams;

		private MaterialPropertyBlock mpb;

		private static readonly int verticesPid    = Shader.PropertyToID("_Vertices");
		private static readonly int vertexCountPid = Shader.PropertyToID("_VertexCount");
		private static readonly int chunkOriginPid = Shader.PropertyToID("_ChunkOrigin");

		protected override void OnCreate()
		{
			mpb = new MaterialPropertyBlock();
		}

		protected override void OnUpdate()
		{
			if (solidMaterial == null && fluidMaterial == null)
			{
				if (SystemAPI.TryGetSingletonEntity<WorldBlockRegistrySingleton>(out Entity registryEntity) &&
				    EntityManager.HasComponent<ChunkMaterialComponent>(registryEntity))
				{
					var matComp = EntityManager.GetComponentData<ChunkMaterialComponent>(registryEntity);
					solidMaterial = matComp.Material;
					fluidMaterial = matComp.Material; // for now, later i will make material for fluids
					renderParams = new RenderParams
					               {
						               renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
						               rendererPriority   = 0,
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
				else
				{
					return;
				}
			}

			foreach ((ChunkGfxBuffers gfx, RefRO<ChunkPositionComponent> pos) in
			         SystemAPI.Query<ChunkGfxBuffers, RefRO<ChunkPositionComponent>>()
			                  .WithAll<IsVisible, HasMesh>())
			{
				if (gfx.VertexBuffer == null) continue;

				int3 wPos = pos.ValueRO.WorldPosition;

				mpb.SetInteger(vertexCountPid, gfx.VertexBuffer.count);
				mpb.SetBuffer(verticesPid, gfx.VertexBuffer);
				mpb.SetVector(chunkOriginPid, new Vector4(wPos.x, wPos.y, wPos.z, 0f));

				var bounds = new Bounds(
				                        new Vector3(wPos.x + 16f, wPos.y + 16f, wPos.z + 16f),
				                        new Vector3(32f, 32f, 32f));

				if (gfx.SolidIndexCount > 0)
				{
					RenderParams rp = renderParams;
					rp.material    = solidMaterial;
					rp.matProps    = mpb;
					rp.worldBounds = bounds;
					Graphics.RenderPrimitivesIndexedIndirect(
					                                         in rp, MeshTopology.Triangles,
					                                         gfx.IndexBuffer, gfx.ArgsBuffer,
					                                         1, 0);
				}

				if (gfx.FluidIndexCount <= 0) continue;
				{
					RenderParams rp = renderParams;
					rp.material    = fluidMaterial;
					rp.matProps    = mpb;
					rp.worldBounds = bounds;
					Graphics.RenderPrimitivesIndexedIndirect(
					                                         in rp, MeshTopology.Triangles,
					                                         gfx.IndexBuffer, gfx.ArgsBuffer,
					                                         1, 1);
				}
			}
		}

		protected override void OnDestroy()
		{
			foreach (ChunkGfxBuffers gfx in SystemAPI.Query<ChunkGfxBuffers>())
				gfx.Dispose();
		}
	}
}