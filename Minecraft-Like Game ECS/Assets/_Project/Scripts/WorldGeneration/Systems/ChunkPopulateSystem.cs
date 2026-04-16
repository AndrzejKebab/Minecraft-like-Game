using System;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(PlayerVisibleChunksSystem))]
	public partial struct ChunkPopulateSystem : ISystem
	{
		private EntityQuery candidateQuery;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<WorldSettingsSingleton>();
			state.RequireForUpdate<ChunkMapSingleton>();

			candidateQuery = SystemAPI.QueryBuilder()
			                          .WithAll<NeedsPopulation, ChunkPositionComponent>()
			                          .WithNone<MarkedToDestroy>()
			                          .Build();
		}

		public void OnUpdate(ref SystemState state)
		{
			if (candidateQuery.IsEmpty) return;

			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
			                                                            SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = Utility.WorldToChunkCoord(playerPos);

			const int budget = GameSettings.CHUNKS_PER_POPULATE_JOB;

			NativeArray<Entity> entities = candidateQuery.ToEntityArray(Allocator.Temp);
			NativeArray<ChunkPositionComponent> positions =
				candidateQuery.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			var urgent = new NativeList<Candidate>(64, Allocator.Temp);
			var normal = new NativeList<Candidate>(entities.Length, Allocator.Temp);

			for (var i = 0; i < entities.Length; i++)
			{
				int3 d        = positions[i].ChunkCoord - playerChunk;
				var  ds       = d.x * d.x + d.y * d.y + d.z * d.z;
				var  isUrgent = math.cmax(math.abs(d)) <= GameSettings.URGENT_RADIUS;

				var c = new Candidate { Entity = entities[i], Position = positions[i], DistSq = ds };
				if (isUrgent) urgent.Add(c);
				else normal.Add(c);
			}

			entities.Dispose();
			positions.Dispose();

			urgent.Sort();
			normal.Sort();

			var take          = math.min(budget, urgent.Length + normal.Length);
			var batchEntities = new NativeArray<Entity>(take, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var batchPositions =
				new NativeArray<ChunkPositionComponent>(take, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

			var written = 0;
			for (var i = 0; i < urgent.Length && written < take; i++, written++)
			{
				batchEntities[written]  = urgent[i].Entity;
				batchPositions[written] = urgent[i].Position;
			}

			for (var i = 0; i < normal.Length && written < take; i++, written++)
			{
				batchEntities[written]  = normal[i].Entity;
				batchPositions[written] = normal[i].Position;
			}

			urgent.Dispose();
			normal.Dispose();

			if (take == 0)
			{
				batchEntities.Dispose();
				batchPositions.Dispose();
				return;
			}

			JobHandle inputDeps = default;
			for (var i = 0; i < take; i++)
			{
				if (!SystemAPI.HasComponent<ChunkActiveJob>(batchEntities[i])) continue;
				JobHandle h = SystemAPI.GetComponent<ChunkActiveJob>(batchEntities[i]).Handle;
				inputDeps = JobHandle.CombineDependencies(inputDeps, h);
			}

			var settings = SystemAPI.GetSingleton<WorldSettingsSingleton>();
			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			var map      = SystemAPI.GetSingleton<ChunkMapSingleton>();
			EntityCommandBuffer.ParallelWriter ecb = SystemAPI
			                                         .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
			                                         .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

			var job = new ChunkPopulateJob
			          {
				          Entities             = batchEntities,
				          Positions            = batchPositions,
				          ChunkDataLookup      = map.ChunkDataLookup,
				          BlockPrototypes      = registry.Blocks,
				          OreTypes             = registry.OreTypes,
				          Seed                 = settings.Seed,
				          ChunkSize            = ChunkData.CHUNK_SIZE,
				          AirID                = registry.Blocks[0].ID,
				          GrassID              = registry.Blocks[3].ID,
				          LogID                = registry.Blocks[7].ID,
				          LeavesID             = registry.Blocks[10].ID,
				          TreeDensity          = registry.TreeDensity,
				          MinTrunkHeight       = registry.MinTrunkHeight,
				          MaxTrunkHeight       = registry.MaxTrunkHeight,
				          ContinentalnessNoise = settings.ContinentalnessNoise,
				          PeaksAndValleysNoise = settings.PeaksAndValleysNoise,
				          ErosionNoise         = settings.ErosionNoise,
				          RiverNoise           = settings.RiverNoise,
				          CavesNoise           = settings.CavesNoise,
				          ContinentalnessCurve = settings.ContinentalnessCurve,
				          ErosionCurve         = settings.ErosionCurve,
				          PeaksAndValleysCurve = settings.PeaksAndValleysCurve,
				          ECB                  = ecb
			          };

			JobHandle handle = job.ScheduleByRef(take, 1, inputDeps);
			for (var i = 0; i < take; i++)
			{
				if (!SystemAPI.HasComponent<ChunkActiveJob>(batchEntities[i])) continue;
				SystemAPI.SetComponent(batchEntities[i], new ChunkActiveJob { Handle = handle });
			}

			// Tell ECB system to wait for handle before playback.
			state.Dependency = JobHandle.CombineDependencies(state.Dependency, handle);

			batchEntities.Dispose(handle);
			batchPositions.Dispose(handle);

			JobHandle.ScheduleBatchedJobs();
		}

		private struct Candidate : IComparable<Candidate>
		{
			public Entity                 Entity;
			public ChunkPositionComponent Position;
			public int                    DistSq;

			public int CompareTo(Candidate other)
			{
				return DistSq.CompareTo(other.DistSq);
			}
		}
	}
}