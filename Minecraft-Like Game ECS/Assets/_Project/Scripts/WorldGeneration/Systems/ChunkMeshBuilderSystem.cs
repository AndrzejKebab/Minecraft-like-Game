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
	[UpdateAfter(typeof(ChunkPopulateSystem))]
	public partial struct ChunkMeshBuilderSystem : ISystem
	{
		private NativeArray<int3> faceChecks;
		private EntityQuery       meshQuery;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<ChunkMapSingleton>();

			faceChecks = new NativeArray<int3>(6, Allocator.Persistent)
			             {
				             [0] = new int3(0, 0, -1), [1] = new int3(0, 0, 1),
				             [2] = new int3(0, 1, 0), [3]  = new int3(0, -1, 0),
				             [4] = new int3(-1, 0, 0), [5] = new int3(1, 0, 0)
			             };

			meshQuery = SystemAPI.QueryBuilder()
			                     .WithAll<NeedsMeshSync, IsPopulated, ChunkPositionComponent, ChunkComponent>()
			                     .WithNone<MarkedToDestroy, IsEmpty>()
			                     .Build();
		}

		public void OnDestroy(ref SystemState state)
		{
			faceChecks.Dispose();
		}

		public void OnUpdate(ref SystemState state)
		{
			if (meshQuery.IsEmpty) return;

			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
			                                                            SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = Utility.WorldToChunkCoord(playerPos);

			var budget = GameSettings.ChunksPerMeshJob;

			NativeHashMap<int3, Entity>     chunkMap        = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			var                             chunkLookup     = SystemAPI.GetSingleton<ChunkMapSingleton>();
			ComponentLookup<IsPopulated>    populatedLookup = SystemAPI.GetComponentLookup<IsPopulated>(true);
			ComponentLookup<UrgentMeshSync> urgentLookup    = SystemAPI.GetComponentLookup<UrgentMeshSync>(true);
			ComponentLookup<ChunkActiveJob> activeJobLookup = SystemAPI.GetComponentLookup<ChunkActiveJob>(true);

			// 1) Pull all candidates, partition into urgent/normal, sort each by distance.
			NativeArray<Entity> entities = meshQuery.ToEntityArray(Allocator.Temp);
			NativeArray<ChunkPositionComponent> positions =
				meshQuery.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			var urgent = new NativeList<Candidate>(64, Allocator.Temp);
			var normal = new NativeList<Candidate>(entities.Length, Allocator.Temp);

			for (var i = 0; i < entities.Length; i++)
			{
				int3 d  = positions[i].ChunkCoord - playerChunk;
				var  ds = d.x * d.x + d.y * d.y + d.z * d.z;
				var  u  = urgentLookup.HasComponent(entities[i]);
				var  c  = new Candidate { Entity = entities[i], Coord = positions[i].ChunkCoord, DistSq = ds};
				if (u) urgent.Add(c);
				else normal.Add(c);
			}

			entities.Dispose();
			positions.Dispose();

			urgent.Sort();
			normal.Sort();

			// Persistent, not TempJob: GreedyMeshJob is only completed via IsCompleted
			// polling in ChunkRenderUploadSystem, so it can outlive the 4-frame TempJob
			// safety window under load. Still disposed deterministically via .Dispose(handle).
			var validEntities  = new NativeArray<Entity>(budget, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
			var validPositions = new NativeArray<int3>(budget, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

			var ecbPre = new EntityCommandBuffer(Allocator.Temp);
			var count  = 0;

			// 2) Drain urgent first, then normal. Skip if any of 6 neighbors not populated.
			count = TryEnqueue(urgent, count, budget, validEntities, validPositions,
			                   ecbPre, chunkMap, chunkLookup, populatedLookup, ref state);
			count = TryEnqueue(normal, count, budget, validEntities, validPositions,
			                   ecbPre, chunkMap, chunkLookup, populatedLookup, ref state);

			urgent.Dispose();
			normal.Dispose();
			ecbPre.Playback(state.EntityManager);
			ecbPre.Dispose();

			if (count == 0)
			{
				validEntities.Dispose();
				validPositions.Dispose();
				return;
			}

			// 3) Build input dependency from per-entity handles of self + 6 neighbors.
			//    De-dup via a small set so we don't combine the same handle multiple times.
			activeJobLookup.Update(ref state);
			JobHandle inputDeps = default;
			var       depSet    = new NativeHashSet<Entity>(count * 7, Allocator.Temp);

			for (var i = 0; i < count; i++)
			{
				Entity self = validEntities[i];
				if (depSet.Add(self) && activeJobLookup.HasComponent(self))
					inputDeps = JobHandle.CombineDependencies(inputDeps, activeJobLookup[self].Handle);

				int3 pos = validPositions[i];
				for (var f = 0; f < 6; f++)
				{
					int3 nPos = pos + faceChecks[f];
					if (!chunkMap.TryGetValue(nPos, out Entity n)) continue;
					if (!depSet.Add(n)) continue;
					if (activeJobLookup.HasComponent(n))
						inputDeps = JobHandle.CombineDependencies(inputDeps, activeJobLookup[n].Handle);
				}
			}

			depSet.Dispose();

			// 4) Schedule mesh job.
			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			EntityCommandBuffer.ParallelWriter ecb = SystemAPI
			                                         .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
			                                         .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

			var job = new GreedyMeshJob
			          {
				          Entities        = validEntities.GetSubArray(0, count),
				          Positions       = validPositions.GetSubArray(0, count),
				          ChunkMap        = chunkMap,
				          BlockDataLookup = chunkLookup.ChunkDataLookup,
				          BlockPrototypes = registry.Blocks,
				          MeshDatas    = registry.Meshes,
				          FaceChecks = faceChecks,
				          ECB             = ecb
			          };

			JobHandle handle = job.ScheduleParallelByRef(count, 1, inputDeps);
			state.Dependency = JobHandle.CombineDependencies(state.Dependency, handle);

			for (var i = 0; i < count; i++)
			{
				if (!SystemAPI.HasComponent<ChunkActiveJob>(validEntities[i])) continue;
				SystemAPI.SetComponent(validEntities[i], new ChunkActiveJob { Handle = handle });
			}

			validEntities.Dispose(handle);
			validPositions.Dispose(handle);

			JobHandle.ScheduleBatchedJobs();
		}

		private int TryEnqueue(
			NativeList<Candidate>        src,           int               count, int budget,
			NativeArray<Entity>          validEntities, NativeArray<int3> validPositions,
			EntityCommandBuffer          ecbPre,
			NativeHashMap<int3, Entity>  chunkMap,
			ChunkMapSingleton            chunkLookup,
			ComponentLookup<IsPopulated> populatedLookup,
			ref SystemState              state)
		{
			for (var i = 0; i < src.Length && count < budget; i++)
			{
				int3   pos = src[i].Coord;
				Entity ent = src[i].Entity;

				// Never enqueue a chunk whose active job is still running — urgent included.
				// A chunk's handle is the whole batch chain (mesh batch → neighbour populate
				// batch → tile batch), so force-completing it below would drag a 100ms+
				// TerraTileGenJob onto the main thread. Skipping keeps the NeedsMeshSync /
				// UrgentMeshSync tags (they're only removed on enqueue), so the chunk is
				// retried next frame; urgency then only affects ordering, never stalls.
				if (state.EntityManager.HasComponent<ChunkActiveJob>(ent))
				{
					if (!state.EntityManager.GetComponentData<ChunkActiveJob>(ent).Handle.IsCompleted)
						continue;
				}

				var ready = true;
				for (var f = 0; f < 6; f++)
				{
					int3 nPos = pos + faceChecks[f];
					if (chunkMap.TryGetValue(nPos, out Entity n) &&
					    populatedLookup.HasComponent(n) &&
					    chunkLookup.ChunkDataLookup.ContainsKey(n) &&
					    chunkLookup.ChunkDataLookup[n].BlockData.IsCreated) continue;
					ready = false;
					break;
				}

				if (!ready) continue;

				validEntities[count]  = ent;
				validPositions[count] = pos;
				count++;

				ecbPre.RemoveComponent<NeedsMeshSync>(ent);
				ecbPre.RemoveComponent<UrgentMeshSync>(ent);

				if (!state.EntityManager.HasComponent<ChunkMeshData>(ent)) continue;
				// Old mesh data must be disposed BEFORE new job overwrites field. The gate
				// above guarantees the handle already reports IsCompleted, so this Complete
				// only releases the fence for the safety system — it never blocks.
				if (state.EntityManager.HasComponent<ChunkActiveJob>(ent))
					state.EntityManager.GetComponentData<ChunkActiveJob>(ent).Handle.Complete();
				state.EntityManager.GetComponentData<ChunkMeshData>(ent).Dispose();
				ecbPre.RemoveComponent<ChunkMeshData>(ent);
			}

			return count;
		}

		private struct Candidate : IComparable<Candidate>
		{
			public Entity Entity;
			public int3   Coord;
			public int    DistSq;

			public int CompareTo(Candidate other)
			{
				return DistSq.CompareTo(other.DistSq);
			}

		}
	}
}