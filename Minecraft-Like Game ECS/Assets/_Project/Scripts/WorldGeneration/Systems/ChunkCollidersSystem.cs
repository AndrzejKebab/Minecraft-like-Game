using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace _Project.WorldGeneration.Systems
{[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial struct ChunkCollidersSystem : ISystem
    {
        private const           int  COLLIDER_RADIUS = 1;

        private CollisionFilter chunkFilter;
        private NativeList<PendingBake> pendingBakes;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Player>();
            pendingBakes = new NativeList<PendingBake>(Allocator.Persistent);
            var chunkLayer = (uint)(1 << LayerMask.NameToLayer("Chunk"));
            chunkFilter = new CollisionFilter
                          {
                              BelongsTo    = chunkLayer,
                              CollidesWith = ~0u
                          };
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (PendingBake b in pendingBakes)
            {
                b.Handle.Complete();
                if (!b.Collider.IsCreated) continue;
                if (b.Collider[0].IsCreated) b.Collider[0].Dispose();
                b.Collider.Dispose();
            }

            if (pendingBakes.IsCreated)
            {
                pendingBakes.Dispose();
            }
        }

        public void CancelPendingBakeFor(Entity entity)
        {
            for (var i = pendingBakes.Count - 1; i >= 0; i--)
            {
                if (pendingBakes[i].Entity != entity) continue;
                PendingBake b = pendingBakes[i];
                b.Handle.Complete();
                if (b.Collider[0].IsCreated) b.Collider[0].Dispose();
                b.Collider.Dispose();
                pendingBakes.RemoveAt(i);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            float3 playerPos = SystemAPI
                .GetComponentRO<LocalTransform>(SystemAPI.GetSingletonEntity<Player>())
                .ValueRO.Position;
            int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);

            var ecb                  = new EntityCommandBuffer(Allocator.Temp);
            var oldCollidersToDispose = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);

            for (var i = pendingBakes.Count - 1; i >= 0; i--)
            {
                PendingBake b = pendingBakes[i];
                if (!b.Handle.IsCompleted) continue;
                b.Handle.Complete();

                if (state.EntityManager.Exists(b.Entity))
                {
                    if (b.Collider[0].IsCreated)
                    {
                        if (state.EntityManager.HasComponent<PhysicsCollider>(b.Entity))
                        {
                            var old = state.EntityManager.GetComponentData<PhysicsCollider>(b.Entity);
                            if (old.Value.IsCreated) oldCollidersToDispose.Add(old.Value);
                            ecb.SetComponent(b.Entity, new PhysicsCollider { Value = b.Collider[0] });
                        }
                        else
                        {
                            ecb.AddComponent(b.Entity, new PhysicsCollider { Value = b.Collider[0] });
                            ecb.AddSharedComponent(b.Entity, new PhysicsWorldIndex { Value = 0 });
                            ecb.AddComponent<HasCollider>(b.Entity);
                        }
                    }
                    else if (state.EntityManager.HasComponent<PhysicsCollider>(b.Entity))
                    {
                        var old = state.EntityManager.GetComponentData<PhysicsCollider>(b.Entity);
                        if (old.Value.IsCreated) oldCollidersToDispose.Add(old.Value);
                        ecb.RemoveComponent<PhysicsCollider>(b.Entity);
                        ecb.RemoveComponent<HasCollider>(b.Entity);
                    }
                }
                else
                {
                    if (b.Collider[0].IsCreated) b.Collider[0].Dispose();
                }

                b.Collider.Dispose();
                pendingBakes.RemoveAt(i);
            }

            foreach ((RefRO<ChunkMeshData> meshData, RefRO<ChunkPositionComponent> pos, Entity entity) in
                     SystemAPI.Query<RefRO<ChunkMeshData>, RefRO<ChunkPositionComponent>>()
                              .WithAll<IsVisible, HasMesh, NeedsColliderSync>()
                              .WithEntityAccess())
            {
                if (pendingBakes.Count >= GameSettings.MAX_CONCURRENT_JOBS) break;
                if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;

                NativeMesh solid = meshData.ValueRO.SolidMesh;
                if (!solid.IsCreated || solid.Vertices.Length == 0) continue;

                var alreadyPending = false;
                foreach (PendingBake b in pendingBakes)
                    if (b.Entity == entity) { alreadyPending = true; break; }
                if (alreadyPending) continue;

                var collider = new NativeArray<BlobAssetReference<Collider>>(1, Allocator.Persistent);
                var job = new ColliderBakeJob
                {
                    SolidVertices = solid.Vertices,
                    SolidIndices  = solid.Triangles,
                    Collider      = collider,
                    Filter        = chunkFilter
                };

                pendingBakes.Add(new PendingBake
                {
                    Entity   = entity,
                    Handle   = job.Schedule(),
                    Collider = collider
                });

                ecb.RemoveComponent<NeedsColliderSync>(entity);
            }

            foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in
                     SystemAPI.Query<RefRO<ChunkPositionComponent>>()
                              .WithAll<HasCollider>()
                              .WithEntityAccess())
            {
                if (IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;

                if (state.EntityManager.HasComponent<PhysicsCollider>(entity))
                {
                    var phys = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                    if (phys.Value.IsCreated) oldCollidersToDispose.Add(phys.Value);
                }

                ecb.RemoveComponent<PhysicsCollider>(entity);
                ecb.RemoveComponent<PhysicsWorldIndex>(entity);
                ecb.RemoveComponent<HasCollider>(entity);

                if (!state.EntityManager.HasComponent<NeedsColliderSync>(entity))
                    ecb.AddComponent<NeedsColliderSync>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (BlobAssetReference<Collider> c in oldCollidersToDispose) c.Dispose();
            oldCollidersToDispose.Dispose();
        }

        private static bool IsChebyshevNear(in int3 a, in int3 b, int radius)
        {
            int3 d = math.abs(a - b);
            return d.x <= radius && d.y <= radius && d.z <= radius;
        }

        private struct PendingBake
        {
            public Entity                                    Entity;
            public JobHandle                                 Handle;
            public NativeArray<BlobAssetReference<Collider>> Collider;
        }
    }
}