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
    // A custom physics collector that entirely ignores the player's own colliders
    public struct ChunkOnlyCollector : ICollector<Unity.Physics.RaycastHit>
    {
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }
        public Unity.Physics.RaycastHit ClosestHit;

        private ComponentLookup<ChunkComponent> m_ChunkLookup;

        public ChunkOnlyCollector(ComponentLookup<ChunkComponent> chunkLookup)
        {
            MaxFraction = 1f;
            NumHits = 0;
            ClosestHit = default;
            m_ChunkLookup = chunkLookup;
        }

        public bool AddHit(Unity.Physics.RaycastHit hit)
        {
            if (!m_ChunkLookup.HasComponent(hit.Entity)) return false;
            
            MaxFraction = hit.Fraction;
            ClosestHit = hit;
            NumHits = 1;
            return true;
        }
    }[UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerInteractionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WorldBlockRegistrySingleton>();
            RequireForUpdate<ChunkMapSingleton>();
        }

        protected override void OnUpdate()
        {
            var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
            int maxBlockID = registry.Blocks.Length - 1;

            var popSystem = World.GetExistingSystemManaged<ChunkPopulateSystem>();
            var meshSystem = World.GetExistingSystemManaged<ChunkMeshBuilderSystem>();
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var chunkLookup = SystemAPI.GetComponentLookup<ChunkComponent>(true);

            foreach (var (interactState, player) in SystemAPI.Query<RefRW<PlayerInteractionState>, RefRO<FirstPersonPlayer>>())
            {
                if (maxBlockID >= 1)
                {
                    if (interactState.ValueRO.ScrollDelta > 0)
                    {
                        interactState.ValueRW.SelectedBlockID++;
                        if (interactState.ValueRW.SelectedBlockID > maxBlockID) interactState.ValueRW.SelectedBlockID = 1;
                    }
                    else if (interactState.ValueRO.ScrollDelta < 0)
                    {
                        interactState.ValueRW.SelectedBlockID--;
                        if (interactState.ValueRW.SelectedBlockID < 1) interactState.ValueRW.SelectedBlockID = (ushort)maxBlockID;
                    }
                }

                if (!interactState.ValueRO.BreakPressed && !interactState.ValueRO.PlacePressed) continue;

                if (!SystemAPI.HasComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter)) continue;
                var viewEntity = SystemAPI.GetComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter).ViewEntity;
                if (!SystemAPI.HasComponent<LocalToWorld>(viewEntity)) continue;

                var viewLtw = SystemAPI.GetComponent<LocalToWorld>(viewEntity);
                var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

                var input = new RaycastInput()
                            {
                                Start = viewLtw.Position,
                                End = viewLtw.Position + viewLtw.Forward * 6f, // 6 block reach
                                Filter = CollisionFilter.Default
                            };

                var collector = new ChunkOnlyCollector(chunkLookup);
                collisionWorld.CastRay(input, ref collector);

                if (collector.NumHits <= 0) continue;
                RaycastHit hit = collector.ClosestHit;
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
            int3 worldInt = new int3((int)math.floor(worldPos.x), (int)math.floor(worldPos.y), (int)math.floor(worldPos.z));
            int3 localPos = worldInt - chunkCoord * VoxelData.CHUNK_SIZE;

            var chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
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