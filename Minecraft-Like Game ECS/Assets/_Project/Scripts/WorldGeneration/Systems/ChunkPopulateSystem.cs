using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using FastNoise2.Bindings;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))][UpdateAfter(typeof(PlayerVisibleChunksSystem))]
	public partial struct ChunkPopulateSystem : ISystem
	{
		private NativeList<ActiveJob> activeJobs;
		private bool                  isNoiseInitialized;
		private FastNoise             noise;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<WorldSettingsSingleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			activeJobs = new NativeList<ActiveJob>(Allocator.Persistent);
		}

		public void OnDestroy(ref SystemState state)
		{
			if (isNoiseInitialized && noise.IsCreated)
				noise.Dispose();
			if (activeJobs.IsCreated)
			{
				activeJobs.Dispose();
			}
		}

		public JobHandle GetChunkDependency(Entity chunkEntity)
		{
			foreach (ActiveJob job in activeJobs)
			{
				if (job.Entity == chunkEntity) return job.Handle;
			}

			return default;
		}

		public void OnUpdate(ref SystemState state)
		{
			if (!SystemAPI.TryGetSingleton(out WorldSettingsSingleton settings)) return;
			if (!isNoiseInitialized)
			{
				InitializeFastNoise(ref settings);
			}

			ProcessJobs(ref state);

			if (activeJobs.Count >= GameSettings.MAX_CONCURRENT_JOBS) return;
			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();

			EntityManager em = state.EntityManager;

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
				{
					if (job.Entity != e) continue;
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
				var blockData =
					new NativeArray<BlockState>(VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE,
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

		private bool InitializeFastNoise(ref WorldSettingsSingleton settings)
		{
			var nodeTree = settings.EncodedNodeTree.ToString();
			noise              = FastNoise.FromEncodedNodeTree(nodeTree);
			isNoiseInitialized = true;

			return false;
		}

		private void ProcessJobs(ref SystemState state)
		{
			EntityManager em = state.EntityManager;
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

					if (!job.IsDirty.Value) em.AddComponentData(job.Entity, new IsEmpty());
					else em.AddComponentData(job.Entity, new NeedsMeshSync());

					job.IsDirty.Dispose();
				}
				else
				{
					job.BlockData.Dispose();
					job.IsDirty.Dispose();
				}

				activeJobs.RemoveAt(i);
			}
		}

		private struct ActiveJob
		{
			public Entity                  Entity;
			public JobHandle               Handle;
			public NativeArray<BlockState> BlockData;
			public NativeReference<bool>   IsDirty;
		}
	}
}