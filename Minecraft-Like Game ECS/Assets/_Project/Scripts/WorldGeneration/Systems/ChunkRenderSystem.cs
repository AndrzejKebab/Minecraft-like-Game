using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class ChunkRenderSystem : SystemBase
    {
        private          Material              solidMaterial;
        private          Material              waterMaterial;
        private          RenderParams          renderParams;
        private readonly Plane[]               frustumPlanes = new Plane[6]; // Added for culling

        protected override void OnCreate()
        {
        }

        protected override void OnUpdate()
        {
            if (solidMaterial == null && waterMaterial == null)
            {
                if (SystemAPI.TryGetSingletonEntity<WorldBlockRegistrySingleton>(out Entity registryEntity) &&
                    EntityManager.HasComponent<ChunkMaterialComponent>(registryEntity))
                {
                    var matComp = EntityManager.GetComponentData<ChunkMaterialComponent>(registryEntity);
                    solidMaterial = matComp.SolidMaterial;
                    waterMaterial = matComp.WaterMaterial;
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
                                   };
                }
                else
                {
                    return;
                }
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                GeometryUtility.CalculateFrustumPlanes(mainCam, frustumPlanes);
            }

            foreach ((ChunkGfxBuffers gfx, RefRO<ChunkPositionComponent> pos) in
                     SystemAPI.Query<ChunkGfxBuffers, RefRO<ChunkPositionComponent>>()
                              .WithAll<IsInViewRange, HasMesh>())
            {
                if (gfx.VertexBuffer == null) continue;

                int3 wPos = pos.ValueRO.WorldPosition;

                var bounds = new Bounds(new Vector3(wPos.x + 16f, wPos.y + 16f, wPos.z + 16f),
                                        new Vector3(32f, 32f, 32f));
                if (mainCam != null && !GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) continue;
                
                if (gfx.SolidIndexCount > 0)
                {
                    RenderParams rp = renderParams;
                    rp.material    = solidMaterial;
                    rp.matProps    = gfx.Mpb;          // <- per-chunk MPB
                    rp.worldBounds = bounds;
                    Graphics.RenderPrimitivesIndexedIndirect(in rp, MeshTopology.Triangles,
                                                             gfx.IndexBuffer, gfx.ArgsBuffer);
                }

                if (gfx.FluidIndexCount <= 0) continue;
                {
                    RenderParams rp = renderParams;
                    rp.material    = waterMaterial;
                    rp.matProps    = gfx.Mpb;          // <- per-chunk MPB
                    rp.worldBounds = bounds;
                    Graphics.RenderPrimitivesIndexedIndirect(in rp, MeshTopology.Triangles,
                                                             gfx.IndexBuffer, gfx.ArgsBuffer, 1, 1);
                }
            }
        }

        protected override void OnDestroy()
        {
            foreach (ChunkGfxBuffers gfx in SystemAPI.Query<ChunkGfxBuffers>())
            {
                gfx.Dispose();
            }
        }
    }
}