using System.Collections.Generic;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(ChunkPopulateSystem))]
	public partial class ChunkMeshBuilderSystem : SystemBase
	{
		private const int MAX_CONCURRENT_JOBS = 16;

		private readonly List<ActiveJob> activeJobs = new();

		protected override void OnCreate()
		{
			RequireForUpdate<WorldBlockRegistrySingleton>();
			RequireForUpdate<ChunkMapSingleton>();
		}

		// Called by PlayerVisibleChunksSystem before chunks are destroyed to prevent memory corruption
		public void CompleteAllJobs()
		{
			ProcessJobs();
		}

		protected override void OnUpdate()
		{
			// 1. Process and complete finished jobs
			ProcessJobs();

			// 2. Gather chunks that need to be meshed
			if (activeJobs.Count >= MAX_CONCURRENT_JOBS) return;
			var                             registry        = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			NativeHashMap<int3, Entity>     chunkMap        = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			ComponentLookup<IsPopulated>    populatedLookup = SystemAPI.GetComponentLookup<IsPopulated>(true);
			ComponentLookup<ChunkComponent> blockDataLookup = SystemAPI.GetComponentLookup<ChunkComponent>(true);

			var queue = new NativePriorityQueue<Entity>(128, Allocator.Temp);

			foreach ((RefRO<ChunkComponent> _, RefRO<ChunkPositionComponent> posComp,
			          RefRO<ChunkPriorityComponent> priority, Entity entity) in SystemAPI
			                                                                .Query<RefRO<ChunkComponent>,
				                                                                RefRO<ChunkPositionComponent>,
				                                                                RefRO<ChunkPriorityComponent>>()
			                                                                .WithAll<IsPopulated, NeedsRender>()
			                                                                .WithNone<ChunkMeshData>()
			                                                                .WithEntityAccess())
			{
				var alreadyProcessing = false;
				foreach (ActiveJob j in activeJobs)
					if (j.Entity == entity)
					{
						alreadyProcessing = true;
						break;
					}

				if (alreadyProcessing) continue;

				int3 pos            = posComp.ValueRO.ChunkCoord;
				var  neighborsReady = true;
				for (var i = 0; i < 6; i++)
				{
					int3 nPos = pos + GetFaceDirection(i);
					if (chunkMap.TryGetValue(nPos, out Entity nEntity) && populatedLookup.HasComponent(nEntity) &&
					    blockDataLookup.HasComponent(nEntity) && blockDataLookup[nEntity].BlockData.IsCreated) continue;
					neighborsReady = false;
					break;
				}

				if (neighborsReady) queue.Enqueue(entity, priority.ValueRO.Distance);
			}

			if (!queue.IsEmpty)
			{
				var countToSchedule = math.min(MAX_CONCURRENT_JOBS - activeJobs.Count, queue.Count);

				// 3. Schedule New Jobs
				for (var i = 0; i < countToSchedule; i++)
				{
					Entity entity = queue.Dequeue();
					int3   pos    = SystemAPI.GetComponent<ChunkPositionComponent>(entity).ChunkCoord;

					Entity neighborZNeg = chunkMap[pos + new int3(0, 0, -1)];
					Entity neighborZPos = chunkMap[pos + new int3(0, 0, 1)];
					Entity neighborYPos = chunkMap[pos + new int3(0, 1, 0)];
					Entity neighborYNeg = chunkMap[pos + new int3(0, -1, 0)];
					Entity neighborXNeg = chunkMap[pos + new int3(-1, 0, 0)];
					Entity neighborXPos = chunkMap[pos + new int3(1, 0, 0)];

					var solidMesh = new NativeMesh(Allocator.Persistent);
					var transparentMesh = new NativeMesh(Allocator.Persistent);
					var fluidMesh = new NativeMesh(Allocator.Persistent);
					
					
					var job = new BuildMeshJob
					          {
						          Blocks          = blockDataLookup[entity].BlockData,
						          BlockPrototypes = registry.Blocks,
						          Meshes          = registry.Meshes,
						          ChunkSize       = VoxelData.CHUNK_SIZE,

						          NeighborZNeg   = blockDataLookup[neighborZNeg].BlockData,
						          NeighborZPos  = blockDataLookup[neighborZPos].BlockData,
						          NeighborYNeg = blockDataLookup[neighborYNeg].BlockData,
						          NeighborYPos    = blockDataLookup[neighborYPos].BlockData,
						          NeighborXNeg   = blockDataLookup[neighborXNeg].BlockData,
						          NeighborXPos  = blockDataLookup[neighborXPos].BlockData,

						          SolidMesh = solidMesh,
						          TransparentMesh = transparentMesh,
						          FluidMesh = fluidMesh,
					          };

					activeJobs.Add(new ActiveJob
					               {
						               Entity = entity,
						               Handle = job.ScheduleByRef(),
						               SolidMesh = solidMesh,
						               TransparentMesh = transparentMesh,
						               FluidMesh = fluidMesh
					               });
				}

				// 4. Immediately kick off worker threads
				if (countToSchedule > 0)
					JobHandle.ScheduleBatchedJobs();
			}

			queue.Dispose();
		}

		private void ProcessJobs()
		{
			var ecb = new EntityCommandBuffer(Allocator.Temp);
			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				ActiveJob job = activeJobs[i];
				if (!job.Handle.IsCompleted) continue;
				job.Handle.Complete();

				if (EntityManager.Exists(job.Entity))
				{
					// Transfer ownership of the NativeLists to the Component.
					// Do NOT dispose them here.
					ecb.AddComponent(job.Entity, new ChunkMeshData
					                             {
						                             SolidMesh       = job.SolidMesh,
						                             TransparentMesh = job.TransparentMesh,
						                             FluidMesh       = job.FluidMesh
					                             });
					ecb.AddComponent<NeedsMeshSync>(job.Entity);
				}
				else
				{
					job.SolidMesh.Dispose();
					job.TransparentMesh.Dispose();
					job.FluidMesh.Dispose();
				}

				activeJobs.RemoveAt(i);
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
		}

		public static int3 GetFaceDirection(int index)
		{
			return index switch
			       {
				       0 => new int3(0, 0, -1),
				       1 => new int3(0, 0, 1),
				       2 => new int3(0, 1, 0),
				       3 => new int3(0, -1, 0),
				       4 => new int3(-1, 0, 0),
				       5 => new int3(1, 0, 0),
				       _ => int3.zero
			       };
		}

		private struct ActiveJob
		{
			public Entity     Entity;
			public JobHandle  Handle;
			public NativeMesh SolidMesh;
			public NativeMesh TransparentMesh;
			public NativeMesh FluidMesh;
		}
	}
}