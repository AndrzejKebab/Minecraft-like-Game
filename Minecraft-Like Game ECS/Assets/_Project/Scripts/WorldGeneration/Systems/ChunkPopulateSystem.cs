using System.Collections.Generic;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using FastNoise2.Bindings;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	public partial class ChunkPopulateSystem : SystemBase
	{
		private const    int             MAX_CONCURRENT_JOBS = 16;
		private readonly List<ActiveJob> activeJobs         = new();
		private          bool            isNoiseInitialized;
		private          FastNoise       noise;

		protected override void OnCreate()
		{
			RequireForUpdate<WorldSettingsSingleton>();
			RequireForUpdate<WorldBlockRegistrySingleton>();
		}

		protected override void OnDestroy()
		{
			if (isNoiseInitialized && noise.IsCreated)
				noise.Dispose();
		}

		// Called by PlayerVisibleChunksSystem before chunks are destroyed to prevent memory corruption
		public void CompleteAllJobs()
		{
			ProcessJobs(true);
		}

		protected override void OnUpdate()
		{
			if (!SystemAPI.TryGetSingleton(out WorldSettingsSingleton settings))
				return;

			if (!isNoiseInitialized)
			{
				var nodeTree = settings.EncodedNodeTree.ToString();
				noise               = FastNoise.FromEncodedNodeTree(nodeTree);
				isNoiseInitialized = true;
			}

			// 1. Process and complete finished jobs
			ProcessJobs(false);

			// 2. Gather Entities and Schedule New Jobs
			if (activeJobs.Count >= MAX_CONCURRENT_JOBS) return;
			var           registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			EntityManager em       = EntityManager;

			EntityQuery query = SystemAPI.QueryBuilder()
			                             .WithAll<IsVisible, ChunkPositionComponent, ChunkComponent,
				                             ChunkPriorityComponent>()
			                             .WithNone<IsPopulated>()
			                             .Build();

			if (query.IsEmpty) return;
			NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
			NativeArray<ChunkPriorityComponent> priorities =
				query.ToComponentDataArray<ChunkPriorityComponent>(Allocator.Temp);

			var queue = new NativePriorityQueue<Entity>(entities.Length, Allocator.Temp);

			for (var i = 0; i < entities.Length; i++)
			{
				Entity e                 = entities[i];
				var    alreadyProcessing = false;
				foreach (ActiveJob job in activeJobs)
					if (job.Entity == e)
					{
						alreadyProcessing = true;
						break;
					}

				if (!alreadyProcessing) queue.Enqueue(e, priorities[i].Distance);
			}

			var countToSchedule = math.min(MAX_CONCURRENT_JOBS - activeJobs.Count, queue.Count);

			for (var i = 0; i < countToSchedule; i++)
			{
				Entity e = queue.Dequeue();

				int3 chunkWorldPos = em.GetComponentData<ChunkPositionComponent>(e).WorldPosition;
				var blockData =
					new NativeArray<ushort>(VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE,
					                        Allocator.Persistent);

				var populateJob = new PopulateChunkJob
				                  {
					                  BlockData       = blockData,
					                  BlockPrototypes = registry.Blocks,
					                  Noise           = noise,
					                  ChunkWorldPos   = chunkWorldPos,
					                  ChunkSize       = VoxelData.CHUNK_SIZE,
					                  BiomeHeight     = settings.BiomeHeight,
					                  Seed            = settings.Seed
				                  };

				activeJobs.Add(new ActiveJob
				               {
					               Entity    = e,
					               BlockData = blockData,
					               Handle = populateJob
						               .ScheduleByRef()
				               });
			}

			queue.Dispose();
			entities.Dispose();
			priorities.Dispose();

			// 3. Immediately kick off worker threads
			if (countToSchedule > 0)
				JobHandle.ScheduleBatchedJobs();
		}

		private void ProcessJobs(bool forceWait)
		{
			EntityManager em = EntityManager;
			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				ActiveJob job = activeJobs[i];
				if (!forceWait && !job.Handle.IsCompleted) continue;
				job.Handle.Complete();

				if (em.Exists(job.Entity))
				{
					var comp = em.GetComponentData<ChunkComponent>(job.Entity);
					comp.BlockData = job.BlockData;
					em.SetComponentData(job.Entity, comp);
					em.AddComponentData(job.Entity, new IsPopulated());
				}
				else
				{
					job.BlockData.Dispose();
				}

				activeJobs.RemoveAt(i);
			}
		}

		private struct ActiveJob
		{
			public Entity              Entity;
			public JobHandle           Handle;
			public NativeArray<ushort> BlockData;
		}
	}
}