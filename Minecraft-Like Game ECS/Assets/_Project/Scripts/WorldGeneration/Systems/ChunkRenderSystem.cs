using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class ChunkRenderSystem : SystemBase
    {
        private BatchRendererGroup brg;
        private BatchMaterialID solidMatID;
        private BatchMaterialID fluidMatID;
        private BatchID batchID;

        private NativeList<BatchMeshID> solidMeshes;
        private NativeList<BatchMeshID> fluidMeshes;
        private NativeList<Bounds> chunkBounds;

        protected override void OnCreate()
        {
            solidMeshes = new NativeList<BatchMeshID>(Allocator.Persistent);
            fluidMeshes = new NativeList<BatchMeshID>(Allocator.Persistent);
            chunkBounds = new NativeList<Bounds>(Allocator.Persistent);
            
            brg = new BatchRendererGroup(OnPerformCulling, System.IntPtr.Zero);
            
            // Tell Unity that this BRG spans the whole world, otherwise OnPerformCulling is never called
            brg.SetGlobalBounds(new Bounds(Vector3.zero, new Vector3(1000000f, 1000000f, 1000000f)));
        }

        protected override void OnUpdate()
        {
            if (solidMatID == BatchMaterialID.Null)
            {
                if (SystemAPI.TryGetSingletonEntity<WorldBlockRegistrySingleton>(out Entity registryEntity) &&
                    EntityManager.HasComponent<ChunkMaterialComponent>(registryEntity))
                {
                    var matComp = EntityManager.GetComponentData<ChunkMaterialComponent>(registryEntity);
                    solidMatID = brg.RegisterMaterial(matComp.SolidMaterial);
                    fluidMatID = brg.RegisterMaterial(matComp.WaterMaterial);
                    batchID = brg.AddBatch(new NativeArray<MetadataValue>(0, Allocator.Temp), default(GraphicsBufferHandle));
                }
                else return;
            }

            solidMeshes.Clear();
            fluidMeshes.Clear();
            chunkBounds.Clear();

            // Register new meshes and populate drawing lists
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach ((ChunkRendererData renderer, Entity entity) in SystemAPI.Query<ChunkRendererData>().WithEntityAccess())
            {
                if (renderer.BatchMeshID == BatchMeshID.Null && renderer.Mesh != null)
                {
                    renderer.BatchMeshID = brg.RegisterMesh(renderer.Mesh);
                }

                if (renderer.BatchMeshID != BatchMeshID.Null)
                {
                    if (renderer.SolidIndexCount > 0) solidMeshes.Add(renderer.BatchMeshID);
                    else solidMeshes.Add(BatchMeshID.Null);

                    if (renderer.FluidIndexCount > 0) fluidMeshes.Add(renderer.BatchMeshID);
                    else fluidMeshes.Add(BatchMeshID.Null);

                    chunkBounds.Add(renderer.Mesh.bounds);
                }
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }[BurstCompile]
        private unsafe struct CullAndDrawJob : IJob
        {
            [ReadOnly] public NativeArray<Plane> Planes;
            [ReadOnly] public NativeArray<BatchMeshID> SolidMeshes;
            [ReadOnly] public NativeArray<BatchMeshID> FluidMeshes;
            [ReadOnly] public NativeArray<Bounds> Bounds;

            public BatchMaterialID SolidMaterial;
            public BatchMaterialID FluidMaterial;
            public BatchID BatchID;
            
            [NativeDisableUnsafePtrRestriction] 
            public BatchCullingOutputDrawCommands* Output;

            public void Execute()
            {
                int maxDraws = SolidMeshes.Length * 2;
                
                // Allocate unmanaged arrays for the BRG outputs (Unity automatically cleans these TempJob allocations up)
                Output->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(sizeof(BatchDrawCommand) * maxDraws, UnsafeUtility.AlignOf<BatchDrawCommand>(), Allocator.TempJob);
                Output->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(sizeof(BatchDrawRange) * 1, UnsafeUtility.AlignOf<BatchDrawRange>(), Allocator.TempJob);
                Output->visibleInstances = (int*)UnsafeUtility.Malloc(sizeof(int) * maxDraws, UnsafeUtility.AlignOf<int>(), Allocator.TempJob);

                int drawCount = 0;

                for (int i = 0; i < Bounds.Length; i++)
                {
                    // Pure Burst Frustum Culling!
                    if (IsVisible(Planes, Bounds[i]))
                    {
                        if (SolidMeshes[i] != BatchMeshID.Null)
                        {
                            Output->visibleInstances[drawCount] = 0;
                            Output->drawCommands[drawCount] = new BatchDrawCommand
                            {
                                visibleOffset = (uint)drawCount,
                                visibleCount = 1,
                                batchID = BatchID,
                                materialID = SolidMaterial,
                                meshID = SolidMeshes[i],
                                submeshIndex = 0,
                                splitVisibilityMask = 0xff,
                                flags = BatchDrawCommandFlags.None
                            };
                            drawCount++;
                        }

                        if (FluidMeshes[i] != BatchMeshID.Null)
                        {
                            Output->visibleInstances[drawCount] = 0;
                            Output->drawCommands[drawCount] = new BatchDrawCommand
                            {
                                visibleOffset = (uint)drawCount,
                                visibleCount = 1,
                                batchID = BatchID,
                                materialID = FluidMaterial,
                                meshID = FluidMeshes[i],
                                submeshIndex = 1,
                                splitVisibilityMask = 0xff,
                                flags = BatchDrawCommandFlags.None
                            };
                            drawCount++;
                        }
                    }
                }

                // Push counts to Culling Output
                Output->drawCommandCount = drawCount;
                Output->visibleInstanceCount = drawCount;
                Output->drawRangeCount = 1;
                
                // CRITICAL: DrawRange tells Unity what rendering layers and filters to apply to the generated commands. Without it, drawing is dropped.
                Output->drawRanges[0] = new BatchDrawRange
                {
                    drawCommandsBegin = 0,
                    drawCommandsCount = (uint)drawCount,
                    filterSettings = new BatchFilterSettings { renderingLayerMask = 1, layer = 0 }
                };
            }

            // High performance, mathematical AABB vs Frustum Plane culling
            private bool IsVisible(NativeArray<Plane> planes, Bounds bounds)
            {
                float3 center = bounds.center;
                float3 extents = bounds.extents;

                for (int i = 0; i < planes.Length; i++)
                {
                    float3 normal = planes[i].normal;
                    float dist = planes[i].distance;

                    // Projection interval radius
                    float r = extents.x * math.abs(normal.x) +
                              extents.y * math.abs(normal.y) +
                              extents.z * math.abs(normal.z);

                    // Distance from box center to plane
                    float d = math.dot(normal, center) + dist;

                    // If completely outside the frustum plane, it's not visible
                    if (d < -r)
                        return false;
                }
                return true;
            }
        }

        private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, System.IntPtr userContext)
        {
            if (solidMeshes.Length == 0) return new JobHandle();

            var job = new CullAndDrawJob
            {
                Planes = cullingContext.cullingPlanes,
                SolidMeshes = solidMeshes.AsArray(),
                FluidMeshes = fluidMeshes.AsArray(),
                Bounds = chunkBounds.AsArray(),
                SolidMaterial = solidMatID,
                FluidMaterial = fluidMatID,
                BatchID = batchID,
                Output = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr()
            };

            return job.Schedule();
        }

        protected override void OnDestroy()
        {
            foreach (ChunkRendererData renderer in SystemAPI.Query<ChunkRendererData>())
            {
                if (renderer.BatchMeshID != BatchMeshID.Null) brg.UnregisterMesh(renderer.BatchMeshID);
                renderer.Dispose();
            }

            if (solidMatID != BatchMaterialID.Null)
            {
                brg.UnregisterMaterial(solidMatID);
                brg.UnregisterMaterial(fluidMatID);
            }

            if (solidMeshes.IsCreated) solidMeshes.Dispose();
            if (fluidMeshes.IsCreated) fluidMeshes.Dispose();
            if (chunkBounds.IsCreated) chunkBounds.Dispose();

            brg?.Dispose();
        }
    }
}