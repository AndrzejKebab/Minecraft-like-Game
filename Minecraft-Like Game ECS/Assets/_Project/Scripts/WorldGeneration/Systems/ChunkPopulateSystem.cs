using System.Collections.Generic;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
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
		private readonly List<ActiveJob> activeJobs = new();
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

		public JobHandle GetChunkDependency(Entity chunkEntity)
		{
			foreach (ActiveJob job in activeJobs)
				if (job.Entity == chunkEntity)
					return job.Handle;
			return default;
		}

		protected override void OnUpdate()
		{
			if (!SystemAPI.TryGetSingleton(out WorldSettingsSingleton settings))
				return;

			if (!isNoiseInitialized)
			{
				var nodeTree = settings.EncodedNodeTree.ToString();
				noise              = FastNoise.FromEncodedNodeTree(nodeTree);
				isNoiseInitialized = true;
			}

			ProcessJobs();

			if (activeJobs.Count >= GameSettings.MAX_CONCURRENT_JOBS) return;
			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();

			EntityManager em = EntityManager;

			EntityQuery query = SystemAPI.QueryBuilder()
			                             .WithAll<IsVisible, ChunkPositionComponent, ChunkComponent,
				                             ChunkPriorityComponent>()
			                             .WithNone<IsPopulated, MarkedToDestroy>()
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

			var countToSchedule = math.min(GameSettings.MAX_CONCURRENT_JOBS - activeJobs.Count, queue.Count);

			for (var i = 0; i < countToSchedule; i++)
			{
				Entity e = queue.Dequeue();

				int3 chunkWorldPos = em.GetComponentData<ChunkPositionComponent>(e).WorldPosition;
				var blockData = new NativeArray<ushort>(VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE,
				                                        Allocator.Persistent);
				var isDirty = new NativeReference<bool>(Allocator.Persistent) { Value = false };
				var populateJob = new PopulateChunkJob
				                  {
					                  BlockData            = blockData,
					                  BlockPrototypes      = registry.Blocks,
					                  Noise                = noise,
					                  ChunkWorldPos        = chunkWorldPos,
					                  ChunkSize            = VoxelData.CHUNK_SIZE,
					                  BiomeHeight          = settings.BiomeHeightCurve,
					                  ErosionCurve         = settings.ErosionCurve,
					                  PeaksAndValleysCurve = settings.PeaksAndValleysCurve,
					                  Seed                 = settings.Seed,
					                  IsDirty              = isDirty
				                  };

				activeJobs.Add(new ActiveJob
				               {
					               Entity    = e,
					               Handle    = populateJob.ScheduleByRef(),
					               BlockData = blockData,
					               IsDirty   = isDirty
				               });
			}

			queue.Dispose();
			entities.Dispose();
			priorities.Dispose();

			if (countToSchedule > 0)
				JobHandle.ScheduleBatchedJobs();
		}

		private void ProcessJobs()
		{
			EntityManager em = EntityManager;
			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				ActiveJob job = activeJobs[i];
				if (!job.Handle.IsCompleted) continue;
				job.Handle.Complete();

				if (em.Exists(job.Entity))
				{
					var comp = em.GetComponentData<ChunkComponent>(job.Entity);
					comp.BlockData = job.BlockData;
					em.SetComponentData(job.Entity, comp);
					em.AddComponentData(job.Entity, new IsPopulated());

					if (!job.IsDirty.Value)
						em.AddComponentData(job.Entity, new IsEmpty());
					else
						// Add NeedsMeshSync so the MeshBuilder picks it up!
						em.AddComponentData(job.Entity, new NeedsMeshSync());

					job.IsDirty.Dispose();
				}

				activeJobs.RemoveAt(i);
			}
		}

		private struct ActiveJob
		{
			public Entity                Entity;
			public JobHandle             Handle;
			public NativeArray<ushort>   BlockData;
			public NativeReference<bool> IsDirty;
		}
	}
}