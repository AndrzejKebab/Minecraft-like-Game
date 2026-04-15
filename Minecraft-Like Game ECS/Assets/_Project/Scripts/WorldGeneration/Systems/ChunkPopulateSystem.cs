using _Project.Character;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using FastNoise2.Bindings;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerVisibleChunksSystem))]
    [BurstCompile]
    public partial struct ChunkPopulateSystem : ISystem
    {
        private int       decorationPhase;

        private const int MAX_TERRAIN_PER_FRAME = 128;
        private const int MAX_DECOR_PER_FRAME   = 64;
        private const int CRITICAL_RADIUS       = 2; // Chebyshev chunks around player

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldBlockRegistrySingleton>();
            state.RequireForUpdate<WorldSettingsSingleton>();
            state.RequireForUpdate<ChunkMapSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<WorldSettingsSingleton>();
            var registry  = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
            var map       = SystemAPI.GetSingleton<ChunkMapSingleton>();
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
                SystemAPI.GetSingletonEntity<Player>()).ValueRO.Position;
            int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);

            // =====================================================================
            // 1. TERRAIN + CAVES — priority sorted
            // =====================================================================
            ScheduleTerrainPass(ref state, playerChunk, map, registry, settings, ecbSystem);

            // =====================================================================
            // 2. DECORATION — priority sorted
            // =====================================================================
            ScheduleDecorationPass(ref state, playerChunk, map, registry, settings, ecbSystem);
        }

        private void ScheduleTerrainPass(
            ref SystemState state, int3 playerChunk,
            ChunkMapSingleton map, WorldBlockRegistrySingleton registry,
            WorldSettingsSingleton settings,
            EndSimulationEntityCommandBufferSystem.Singleton ecbSystem)
        {
            // Collect all candidates
            var query = SystemAPI.QueryBuilder()
                .WithAll<NeedsTerrainTag, ChunkPositionComponent>()
                .WithNone<MarkedToDestroy>()
                .Build();

            int candidateCount = query.CalculateEntityCount();
            if (candidateCount == 0) return;

            var candEntities  = query.ToEntityArray(Allocator.TempJob);
            var candPositions = query.ToComponentDataArray<ChunkPositionComponent>(Allocator.TempJob);

            // Sort by squared distance to player chunk
            var sorted = new NativeArray<ChunkCandidate>(candidateCount, Allocator.TempJob);
            for (int i = 0; i < candidateCount; i++)
            {
                int3 d = candPositions[i].ChunkCoord - playerChunk;
                sorted[i] = new ChunkCandidate
                {
                    Entity   = candEntities[i],
                    Position = candPositions[i],
                    DistSq   = d.x * d.x + d.y * d.y + d.z * d.z
                };
            }

            sorted.Sort();

            candEntities.Dispose();
            candPositions.Dispose();

            // Count critical chunks (within CRITICAL_RADIUS) — no budget cap on these
            int criticalCount = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                int3 d = math.abs(sorted[i].Position.ChunkCoord - playerChunk);
                if (math.cmax(d) > CRITICAL_RADIUS) break; // sorted, so rest are farther
                criticalCount++;
            }

            int take = math.max(criticalCount, math.min(MAX_TERRAIN_PER_FRAME, sorted.Length));
            take = math.min(take, sorted.Length);

            var terrainEntities  = new NativeArray<Entity>(take, Allocator.TempJob);
            var terrainPositions = new NativeArray<ChunkPositionComponent>(take, Allocator.TempJob);
            for (int i = 0; i < take; i++)
            {
                terrainEntities[i]  = sorted[i].Entity;
                terrainPositions[i] = sorted[i].Position;
            }
            sorted.Dispose();

            var terrainJob = new TerrainShapePassJob
            {
                Entities             = terrainEntities,
                Positions            = terrainPositions,
                ChunkDataLookup      = map.ChunkDataLookup,
                ContinentalnessNoise = settings.ContinentalnessNoise,
                PeaksAndValleysNoise = settings.PeaksAndValleysNoise,
                ErosionNoise         = settings.ErosionNoise,
                BlockPrototypes      = registry.Blocks,
                BiomeHeight          = settings.ContinentalnessCurve,
                ErosionCurve         = settings.ErosionCurve,
                PeaksAndValleysCurve = settings.PeaksAndValleysCurve,
                Seed                 = settings.Seed,
                ChunkSize            = VoxelData.CHUNK_SIZE,
            };
            
            int batch = math.max(1, take / 16);
            state.Dependency = terrainJob.ScheduleParallelByRef(take, batch, state.Dependency);

            var cavesECB = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var cavesJob = new CavesPassJob
            {
                Entities        = terrainEntities,
                Positions       = terrainPositions,
                ChunkDataLookup = map.ChunkDataLookup,
                BlockPrototypes = registry.Blocks,
                Seed            = settings.Seed,
                ChunkSize       = VoxelData.CHUNK_SIZE,
                CaveNoise       = settings.CavesNoise,
                ECB             = cavesECB
            };
            state.Dependency = cavesJob.ScheduleParallelByRef(take, batch, state.Dependency);

            terrainEntities.Dispose(state.Dependency);
            terrainPositions.Dispose(state.Dependency);
        }

        private void ScheduleDecorationPass(
            ref SystemState state, int3 playerChunk,
            ChunkMapSingleton map, WorldBlockRegistrySingleton registry,
            WorldSettingsSingleton settings,
            EndSimulationEntityCommandBufferSystem.Singleton ecbSystem)
        {
            var query = SystemAPI.QueryBuilder()
                .WithAll<NeedsDecorationTag, ChunkPositionComponent>()
                .WithNone<NeedsTerrainTag, MarkedToDestroy>()
                .Build();

            int candidateCount = query.CalculateEntityCount();
            if (candidateCount == 0) return;

            var candEntities  = query.ToEntityArray(Allocator.TempJob);
            var candPositions = query.ToComponentDataArray<ChunkPositionComponent>(Allocator.TempJob);

            var sorted = new NativeArray<ChunkCandidate>(candidateCount, Allocator.TempJob);
            for (int i = 0; i < candidateCount; i++)
            {
                int3 d = candPositions[i].ChunkCoord - playerChunk;
                sorted[i] = new ChunkCandidate
                {
                    Entity   = candEntities[i],
                    Position = candPositions[i],
                    DistSq   = d.x * d.x + d.y * d.y + d.z * d.z
                };
            }
            
            sorted.Sort();

            candEntities.Dispose();
            candPositions.Dispose();

            int criticalCount = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                int3 d = math.abs(sorted[i].Position.ChunkCoord - playerChunk);
                if (math.cmax(d) > CRITICAL_RADIUS) break;
                criticalCount++;
            }

            int take = math.max(criticalCount, math.min(MAX_DECOR_PER_FRAME, sorted.Length));
            take = math.min(take, sorted.Length);

            var decorEntities  = new NativeArray<Entity>(take, Allocator.TempJob);
            var decorPositions = new NativeArray<ChunkPositionComponent>(take, Allocator.TempJob);
            for (int i = 0; i < take; i++)
            {
                decorEntities[i]  = sorted[i].Entity;
                decorPositions[i] = sorted[i].Position;
            }
            sorted.Dispose();

            var decorECB         = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var dirtiedNeighbors = new NativeQueue<Entity>(Allocator.TempJob);
            var terrainTags      = SystemAPI.GetComponentLookup<NeedsTerrainTag>(true);

            var decorJob = new DecorationJobFor
            {
                ColorPhase       = decorationPhase,
                Entities         = decorEntities,
                Positions        = decorPositions,
                ChunkMap         = map.ChunkMap,
                ChunkDataLookup  = map.ChunkDataLookup,
                OreTypes         = registry.OreTypes,
                Seed             = settings.Seed,
                ChunkSize        = VoxelData.CHUNK_SIZE,
                AirID            = registry.Blocks[0].ID,
                GrassID          = registry.Blocks[3].ID,
                LogID            = registry.Blocks[7].ID,
                LeavesID         = registry.Blocks[10].ID,
                TreeDensity      = registry.TreeDensity,
                MinTrunkHeight   = registry.MinTrunkHeight,
                MaxTrunkHeight   = registry.MaxTrunkHeight,
                DirtiedNeighbors = dirtiedNeighbors.AsParallelWriter(),
                TerrainTagLookup = terrainTags,
                ECB              = decorECB
            };

            decorationPhase = (decorationPhase + 1) & 3;

            int batch = math.max(1, take / 16);
            state.Dependency = decorJob.ScheduleParallelByRef(take, batch, state.Dependency);

            var dirtySyncJob = new ApplyDirtyNeighborsJob { Queue = dirtiedNeighbors, ECB = decorECB };
            state.Dependency = dirtySyncJob.ScheduleByRef(state.Dependency);

            dirtiedNeighbors.Dispose(state.Dependency);
            decorEntities.Dispose(state.Dependency);
            decorPositions.Dispose(state.Dependency);
        }

        private struct ChunkCandidate : System.IComparable<ChunkCandidate>
        {
            public Entity                 Entity;
            public ChunkPositionComponent Position;
            public int                    DistSq;
            public int CompareTo(ChunkCandidate other) => DistSq.CompareTo(other.DistSq);
        }
        
        [BurstCompile]
        public struct ApplyDirtyNeighborsJob : IJob
        {
            public NativeQueue<Entity>                Queue;
            public EntityCommandBuffer.ParallelWriter ECB;
            public void Execute()
            {
                while (Queue.TryDequeue(out Entity e))
                    ECB.AddComponent<NeedsMeshSync>(0, e);
            }
        }
    }
}