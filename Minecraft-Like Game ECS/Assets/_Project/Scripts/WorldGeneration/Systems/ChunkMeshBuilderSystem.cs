using System.Collections.Generic;
using System.Linq;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(ChunkPopulateSystem))]
	public partial class ChunkMeshBuilderSystem : SystemBase
	{
		public static readonly NativeArray<float3> FaceTangents =
			new(6, Allocator.Persistent)
			{
				[0] = new float3(1, 0, 0),
				[1] = new float3(-1, 0, 0),
				[2] = new float3(1, 0, 0),
				[3] = new float3(-1, 0, 0),
				[4] = new float3(0, 0, -1),
				[5] = new float3(0, 0, 1)
			};

		public static readonly NativeArray<float3> FaceChecks =
			new(6, Allocator.Persistent)
			{
				[0] = new float3(0, 0, -1),
				[1] = new float3(0, 0, 1),
				[2] = new float3(0, 1, 0),
				[3] = new float3(0, -1, 0),
				[4] = new float3(-1, 0, 0),
				[5] = new float3(1, 0, 0)
			};

		public static readonly NativeArray<VertexAttributeDescriptor> Layout =
			new(6, Allocator.Persistent)
			{
				[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
				[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
				[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4),
				[3] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UInt8, 4),
				[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
				[5] = new VertexAttributeDescriptor(VertexAttribute.TexCoord7, VertexAttributeFormat.Float16, 4)
			};

		private readonly List<ActiveJob> activeJobs = new();

		protected override void OnCreate()
		{
			RequireForUpdate<WorldBlockRegistrySingleton>();
			RequireForUpdate<ChunkMapSingleton>();
		}

		protected override void OnDestroy()
		{
			foreach (ActiveJob job in activeJobs) job.Handle.Complete();
			FaceChecks.Dispose();
			FaceTangents.Dispose();
			Layout.Dispose();
		}

		public void CancelJobFor(Entity entity)
		{
			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				if (activeJobs[i].Entity != entity) continue;
				ActiveJob job = activeJobs[i];
				job.Handle.Complete();
				job.MeshDataArray.Dispose();
				activeJobs.RemoveAt(i);
			}
		}

		public JobHandle GetChunkDependency(Entity chunkEntity)
		{
			JobHandle result = default;
			foreach (ActiveJob job in activeJobs)
			{
				if (job.Entity == chunkEntity || job.NBack == chunkEntity || job.NFront == chunkEntity ||
				    job.NTop == chunkEntity || job.NBottom == chunkEntity || job.NLeft == chunkEntity ||
				    job.NRight == chunkEntity) result = JobHandle.CombineDependencies(result, job.Handle);
			}

			return result;
		}

		protected override void OnUpdate()
		{
			ProcessJobs();

			if (activeJobs.Count >= GameSettings.MAX_CONCURRENT_JOBS) return;

			var                             registry        = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			NativeHashMap<int3, Entity>     chunkMap        = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			ComponentLookup<IsPopulated>    populatedLookup = SystemAPI.GetComponentLookup<IsPopulated>(true);
			ComponentLookup<ChunkComponent> blockDataLookup = SystemAPI.GetComponentLookup<ChunkComponent>(true);

			var queue           = new NativePriorityQueue<Entity>(128, Allocator.Temp);
			var toStripMeshSync = new NativeList<Entity>(Allocator.Temp);
			foreach ((RefRO<ChunkComponent> _, RefRO<ChunkPositionComponent> posComp,
			          RefRO<ChunkPriorityComponent> priority, Entity entity) in SystemAPI
				         .Query<RefRO<ChunkComponent>,
					         RefRO<ChunkPositionComponent>,
					         RefRO<ChunkPriorityComponent>>()
				         .WithAll<IsPopulated, NeedsMeshSync>()
				         .WithNone<MarkedToDestroy, IsEmpty>()
				         .WithEntityAccess())
			{
				var alreadyProcessing = false;
				foreach (ActiveJob j in activeJobs)
				{
					if (j.Entity != entity) continue;
					alreadyProcessing = true;
					break;
				}

				if (alreadyProcessing) continue;

				int3 pos            = posComp.ValueRO.ChunkCoord;
				var  neighborsReady = true;
				for (var i = 0; i < 6; i++)
				{
					int3 nPos = pos + new int3(FaceChecks[i]);
					if (chunkMap.TryGetValue(nPos, out Entity nEntity) && populatedLookup.HasComponent(nEntity) &&
					    blockDataLookup.HasComponent(nEntity) && blockDataLookup[nEntity].BlockData.IsCreated) continue;
					neighborsReady = false;
					break;
				}

				if (neighborsReady) queue.Enqueue(entity, priority.ValueRO.Distance);
			}

			if (!queue.IsEmpty)
			{
				var countToSchedule = math.min(GameSettings.MAX_CONCURRENT_JOBS - activeJobs.Count, queue.Count);

				for (var i = 0; i < countToSchedule; i++)
				{
					Entity entity = queue.Dequeue();
					int3   pos    = SystemAPI.GetComponent<ChunkPositionComponent>(entity).ChunkCoord;

					Entity neighborZNeg = chunkMap[pos + new int3(0, 0, -1)];
					Entity neighborZPos = chunkMap[pos + new int3(0, 0, 1)];
					Entity neighborYNeg = chunkMap[pos + new int3(0, -1, 0)];
					Entity neighborYPos = chunkMap[pos + new int3(0, 1, 0)];
					Entity neighborXNeg = chunkMap[pos + new int3(-1, 0, 0)];
					Entity neighborXPos = chunkMap[pos + new int3(1, 0, 0)];

					Mesh.MeshDataArray meshData = Mesh.AllocateWritableMeshData(1);
					var job = new BuildMeshJob
					          {
						          Blocks          = blockDataLookup[entity].BlockData,
						          BlockPrototypes = registry.Blocks,
						          Meshes          = registry.Meshes,
						          ChunkSize       = VoxelData.CHUNK_SIZE,

						          NeighborZNeg  = blockDataLookup[neighborZNeg].BlockData,
						          NeighborZPos  = blockDataLookup[neighborZPos].BlockData,
						          NeighborYNeg  = blockDataLookup[neighborYNeg].BlockData,
						          NeighborYPos  = blockDataLookup[neighborYPos].BlockData,
						          NeighborXNeg  = blockDataLookup[neighborXNeg].BlockData,
						          NeighborXPos  = blockDataLookup[neighborXPos].BlockData,
						          Layout        = Layout,
						          FaceChecks    = FaceChecks,
						          FaceTangents  = FaceTangents,
						          MeshDataArray = meshData
					          };

					activeJobs.Add(new ActiveJob
					               {
						               Entity        = entity,
						               NBack         = neighborZNeg, NFront  = neighborZPos,
						               NTop          = neighborYPos, NBottom = neighborYNeg,
						               NLeft         = neighborXNeg, NRight  = neighborXPos,
						               Handle        = job.ScheduleByRef(),
						               MeshDataArray = meshData
					               });
					toStripMeshSync.Add(entity);
				}

				foreach (Entity e in toStripMeshSync)
					EntityManager.RemoveComponent<NeedsMeshSync>(e);
				toStripMeshSync.Dispose();

				if (countToSchedule > 0)
					JobHandle.ScheduleBatchedJobs();
			}

			queue.Dispose();
		}

		private void ProcessJobs()
		{
			var ecb                = new EntityCommandBuffer(Allocator.Temp);
			var processedThisFrame = 0;

			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				ActiveJob job = activeJobs[i];
				if (!job.Handle.IsCompleted) continue;
				job.Handle.Complete();

				if (processedThisFrame >= GameSettings.MAX_CONCURRENT_JOBS) continue;

				if (EntityManager.Exists(job.Entity))
				{
					if (EntityManager.HasComponent<ChunkMeshData>(job.Entity))
					{
						var chunkMeshData = EntityManager.GetComponentData<ChunkMeshData>(job.Entity);
						Mesh.ApplyAndDisposeWritableMeshData(job.MeshDataArray, chunkMeshData.ChunkMesh);
						chunkMeshData.ChunkMesh.bounds =
							new Bounds(new Vector3(16f, 16f, 16f), new Vector3(32, 32, 32));
						if (!EntityManager.HasComponent<NeedsColliderSync>(job.Entity))
							ecb.AddComponent<NeedsColliderSync>(job.Entity);
					}
					else
					{
						var chunkMeshData = new ChunkMeshData { ChunkMesh = new Mesh() };
						Mesh.ApplyAndDisposeWritableMeshData(job.MeshDataArray, chunkMeshData.ChunkMesh);
						chunkMeshData.ChunkMesh.bounds =
							new Bounds(new Vector3(16f, 16f, 16f), new Vector3(32, 32, 32));
						ecb.AddComponent(job.Entity, chunkMeshData);
						if (!EntityManager.HasComponent<HasMesh>(job.Entity))
							ecb.AddComponent<HasMesh>(job.Entity);
						if (!EntityManager.HasComponent<NeedsColliderSync>(job.Entity))
							ecb.AddComponent<NeedsColliderSync>(job.Entity);
					}
				}
				else
				{
					job.MeshDataArray.Dispose();
				}

				activeJobs.RemoveAt(i);
				processedThisFrame++;
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
		}

		private struct ActiveJob
		{
			public Entity             Entity;
			public Entity             NBack, NFront, NTop, NBottom, NLeft, NRight;
			public JobHandle          Handle;
			public Mesh.MeshDataArray MeshDataArray;
		}
	}
}