using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerInteractionSystem : SystemBase
    {
        private static readonly uint chunkLayer     = (uint)(1 << UnityEngine.LayerMask.NameToLayer("Chunk"));
        private static readonly CollisionFilter raycastFilter = new()
                                                                {
                                                                    BelongsTo    = ~0u,
                                                                    CollidesWith = chunkLayer // Raycast ONLY interacts with Chunks!
                                                                };
        
        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WorldBlockRegistrySingleton>();
            RequireForUpdate<ChunkMapSingleton>();
        }

        protected override void OnUpdate()
        {
            var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
            var maxBlockID = registry.Blocks.Length - 1;

            var popSystem = World.GetExistingSystemManaged<ChunkPopulateSystem>();
            var meshSystem = World.GetExistingSystemManaged<ChunkMeshBuilderSystem>();
            
            var                             ecb         = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRW<PlayerInteractionState> interactState, RefRO<FirstPersonPlayer> player) in SystemAPI.Query<RefRW<PlayerInteractionState>, RefRO<FirstPersonPlayer>>())
            {
                if (maxBlockID >= 1)
                {
                    switch (interactState.ValueRO.ScrollDelta)
                    {
                        case > 0:
                        {
                            interactState.ValueRW.SelectedBlockID++;
                            if (interactState.ValueRW.SelectedBlockID > maxBlockID) interactState.ValueRW.SelectedBlockID = 1;
                            break;
                        }
                        case < 0:
                        {
                            interactState.ValueRW.SelectedBlockID--;
                            if (interactState.ValueRW.SelectedBlockID < 1) interactState.ValueRW.SelectedBlockID = (ushort)maxBlockID;
                            break;
                        }
                    }
                }

                if (interactState.ValueRO is { BreakPressed: false, PlacePressed: false }) continue;

                if (!SystemAPI.HasComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter)) continue;
                Entity viewEntity = SystemAPI.GetComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter).ViewEntity;
                if (!SystemAPI.HasComponent<LocalToWorld>(viewEntity)) continue;

                var  viewLtw        = SystemAPI.GetComponent<LocalToWorld>(viewEntity);
                CollisionWorld  collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;
                
                var input = new RaycastInput()
                            {
                                Start = viewLtw.Position,
                                End = viewLtw.Position + viewLtw.Forward * 6f, // 6 block reach
                                Filter = raycastFilter
                            };

                if (!collisionWorld.CastRay(input, out RaycastHit hit)) continue;
                if (interactState.ValueRO.BreakPressed)
                {
                    float3 blockPos = hit.Position - hit.SurfaceNormal * 0.01f;
                    ModifyBlock(blockPos, 0, ecb, popSystem, meshSystem);
                }
                else if (interactState.ValueRO.PlacePressed)
                {
                    float3 blockPos = hit.Position + hit.SurfaceNormal * 0.01f;
                    ModifyBlock(blockPos, interactState.ValueRO.SelectedBlockID, ecb, popSystem, meshSystem);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ModifyBlock(float3 worldPos, ushort newBlockID, EntityCommandBuffer ecb, ChunkPopulateSystem popSystem, ChunkMeshBuilderSystem meshSystem)
        {
            int3 chunkCoord = PlayerVisibleChunksSystem.WorldToChunkCoord(worldPos);
            var worldInt = new int3((int)math.floor(worldPos.x), (int)math.floor(worldPos.y), (int)math.floor(worldPos.z));
            int3 localPos = worldInt - chunkCoord * VoxelData.CHUNK_SIZE;

            NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
            if (!chunkMap.TryGetValue(chunkCoord, out Entity chunkEntity)) return;

            JobHandle popHandle = popSystem?.GetChunkDependency(chunkEntity) ?? default;
            JobHandle meshHandle = meshSystem?.GetChunkDependency(chunkEntity) ?? default;
            JobHandle.CombineDependencies(popHandle, meshHandle).Complete();

            var chunkComp = SystemAPI.GetComponent<ChunkComponent>(chunkEntity);

            if (localPos.x < 0 || localPos.x >= VoxelData.CHUNK_SIZE || localPos.y < 0 || localPos.y >= VoxelData.CHUNK_SIZE || localPos.z < 0 || localPos.z >= VoxelData.CHUNK_SIZE) return;

            chunkComp.BlockData.SetAtIndex(localPos.x, localPos.y, localPos.z, newBlockID);

            if (newBlockID != 0) 
            {
                ecb.RemoveComponent<IsEmpty>(chunkEntity);
            }

            ecb.AddComponent<NeedsMeshSync>(chunkEntity);

            switch (localPos.x)
            {
                case 0:
                    TryMarkNeighbor(chunkCoord + new int3(-1, 0, 0), ecb);
                    break;
                case VoxelData.CHUNK_SIZE - 1:
                    TryMarkNeighbor(chunkCoord + new int3(1, 0, 0), ecb);
                    break;
            }
            switch (localPos.y)
            {
                case 0:
                    TryMarkNeighbor(chunkCoord + new int3(0, -1, 0), ecb);
                    break;
                case VoxelData.CHUNK_SIZE - 1:
                    TryMarkNeighbor(chunkCoord + new int3(0, 1, 0), ecb);
                    break;
            }
            switch (localPos.z)
            {
                case 0:
                    TryMarkNeighbor(chunkCoord + new int3(0, 0, -1), ecb);
                    break;
                case VoxelData.CHUNK_SIZE - 1:
                    TryMarkNeighbor(chunkCoord + new int3(0, 0, 1), ecb);
                    break;
            }
            switch (localPos.x)
            {
                case 0:
                    TryMarkNeighbor(chunkCoord + new int3(-1, 0, 0), ecb);
                    break;
                case VoxelData.CHUNK_SIZE - 1:
                    TryMarkNeighbor(chunkCoord + new int3(1, 0, 0), ecb);
                    break;
            }
            switch (localPos.y)
            {
                case 0:
                    TryMarkNeighbor(chunkCoord + new int3(0, -1, 0), ecb);
                    break;
                case VoxelData.CHUNK_SIZE - 1:
                    TryMarkNeighbor(chunkCoord + new int3(0, 1, 0), ecb);
                    break;
            }
            switch (localPos.z)
            {
                case 0:
                    TryMarkNeighbor(chunkCoord + new int3(0, 0, -1), ecb);
                    break;
                case VoxelData.CHUNK_SIZE - 1:
                    TryMarkNeighbor(chunkCoord + new int3(0, 0, 1), ecb);
                    break;
            }
        }

        private void TryMarkNeighbor(int3 neighborCoord, EntityCommandBuffer ecb)
        {
            if (SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap.TryGetValue(neighborCoord, out Entity chunkEntity))
            {
                ecb.AddComponent<NeedsMeshSync>(chunkEntity);
            }
        }
    }
}