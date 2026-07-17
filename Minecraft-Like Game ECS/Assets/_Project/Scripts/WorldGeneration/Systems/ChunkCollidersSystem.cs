using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UtilityLibrary.Core;
using Collider = Unity.Physics.Collider;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
	[BurstCompile]
	public partial struct ChunkCollidersSystem : ISystem
	{
		private const int               COLLIDER_RADIUS = 1;
		private       CollisionFilter   chunkFilter;
		private       NativeArray<int3> faceChecks;

		private JobHandle                                 activeJobHandle;
		private NativeArray<Entity>                       activeEntities;
		private NativeArray<int3>                         activePositions;
		private NativeArray<BlobAssetReference<Collider>> activeBlobs;
		private bool                                      isJobActive;
		private int3 lastPlayerChunk;

		private EntityQuery urgentQuery;
		private EntityQuery outOfRangeQuery;

		[BurstDiscard]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<ChunkMapSingleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();

			var layer     = LayerMask.NameToLayer("Chunk");
			var chunkMask = layer == -1 ? 1u << 3 : 1u << layer;
			chunkFilter = new CollisionFilter { BelongsTo = chunkMask, CollidesWith = ~0u };

			faceChecks = new NativeArray<int3>(6, Allocator.Persistent)
			             {
				             [0] = new int3(-1, 0, 0), [1] = new int3(1, 0, 0),
				             [2] = new int3(0, -1, 0), [3] = new int3(0, 1, 0),
				             [4] = new int3(0, 0, -1), [5] = new int3(0, 0, 1)
			             };

			urgentQuery     = SystemAPI.QueryBuilder().WithAll<UrgentColliderSync>().Build();
			outOfRangeQuery = SystemAPI.QueryBuilder().WithAll<ChunkPositionComponent, HasCollider>().Build();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			if (faceChecks.IsCreated) faceChecks.Dispose();
			if (!isJobActive) return;
			activeJobHandle.Complete();
			if (activeEntities.IsCreated) activeEntities.Dispose();
			if (activePositions.IsCreated) activePositions.Dispose();
			if (!activeBlobs.IsCreated) return;
			for (var i = 0; i < activeBlobs.Length; i++)
				if (activeBlobs[i].IsCreated)
					activeBlobs[i].Dispose();
			activeBlobs.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
			                                                            SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = Utility.WorldToChunkCoord(playerPos);

			var ecb          = new EntityCommandBuffer(Allocator.Temp);
			var oldToDispose = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);
			var justBaked    = new NativeHashSet<Entity>(8, Allocator.Temp);

			// ── 1. Complete active job if ready or urgent ──────────────────────
			if(isJobActive)
			{
				oldToDispose = CompleteCurrentJobs(ref state, ecb, ref justBaked, ref oldToDispose);
			}

			var playerMoved = !playerChunk.Equals(lastPlayerChunk);
			var hasUrgent   = !urgentQuery.IsEmptyIgnoreFilter;

			if (playerMoved || hasUrgent)
			{
				if (playerMoved)
				{
					lastPlayerChunk = playerChunk;
					// ── 2. Strip colliders for chunks that left radius ─────────────────
					RemoveOutOfRangeColliders(ref state, ref playerChunk, ref justBaked, ref oldToDispose, ecb);
				}

				// ── 3. Collect candidates and schedule new batch when idle ─────────
				oldToDispose = ScheduleColliderJobs(ref state, ref playerChunk, ref oldToDispose, ecb);
			}

			justBaked.Dispose();
			ecb.Playback(state.EntityManager);
			ecb.Dispose();

			foreach (BlobAssetReference<Collider> c in oldToDispose) c.Dispose();
			oldToDispose.Dispose();
		}

		private NativeList<BlobAssetReference<Collider>> ScheduleColliderJobs(
			ref SystemState                              state,        ref int3            playerChunk,
			ref NativeList<BlobAssetReference<Collider>> oldToDispose, EntityCommandBuffer ecb)
		{
			if (isJobActive) return oldToDispose;
			var                         candEntities  = new NativeList<Entity>(64, Allocator.Temp);
			var                         candPositions = new NativeList<int3>(64, Allocator.Temp);
			NativeHashMap<int3, Entity> chunkMap      = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;

			for (var x = -COLLIDER_RADIUS; x <= COLLIDER_RADIUS; x++)
			{
				for (var y = -COLLIDER_RADIUS; y <= COLLIDER_RADIUS; y++)
				{
					for (var z = -COLLIDER_RADIUS; z <= COLLIDER_RADIUS; z++)
					{
						int3 pos = playerChunk + new int3(x, y, z);
						if (!chunkMap.TryGetValue(pos, out Entity entity)) continue;

						if (!state.EntityManager.HasComponent<IsPopulated>(entity) ||
						    !state.EntityManager.HasComponent<IsInViewRange>(entity)) continue;

						var hasCollider = state.EntityManager.HasComponent<HasCollider>(entity);
						var needsRebake = state.EntityManager.HasComponent<NeedsColliderSync>(entity) ||
						                  state.EntityManager.HasComponent<UrgentColliderSync>(entity);

						if (hasCollider && !needsRebake) continue;

						candEntities.Add(entity);
						candPositions.Add(pos);
					}
				}
			}

			if (candEntities.Length > 0)
			{
				activeEntities  = new NativeArray<Entity>(candEntities.AsArray(), Allocator.Persistent);
				activePositions = new NativeArray<int3>(candPositions.AsArray(), Allocator.Persistent);

				ScheduleBatch(ref state);

				// No same-tick force-complete for urgent chunks: the batch's dependencies
				// chain through neighbour populate/tile batch handles, so Complete() here
				// can drag 100ms+ of terrain generation onto the main thread — inside the
				// fixed-step group, which catches up and repeats the stall within one frame.
				// CompleteCurrentJobs applies the batch as soon as IsCompleted next tick;
				// urgency now means "first in the next batch", never "stall the frame".
			}

			candEntities.Dispose();
			candPositions.Dispose();

			return oldToDispose;
		}

		[BurstCompile]
		private void RemoveOutOfRangeColliders(ref SystemState                              state, ref int3 playerChunk,
		                                       ref NativeHashSet<Entity>                    justBaked,
		                                       ref NativeList<BlobAssetReference<Collider>> oldToDispose,
		                                       EntityCommandBuffer                          ecb)
		{
			NativeArray<Entity> entityArray = outOfRangeQuery.ToEntityArray(Allocator.Temp);
			NativeArray<ChunkPositionComponent> positionArray =
				outOfRangeQuery.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);
			for (var index = 0; index < entityArray.Length; index++)
			{
				Entity                 entity = entityArray[index];
				ChunkPositionComponent pos    = positionArray[index];
				if (IsChebyshevNear(pos.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;
				if (justBaked.Contains(entity)) continue;

				if (state.EntityManager.HasComponent<PhysicsCollider>(entity))
				{
					var phys = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
					if (phys.Value.IsCreated) oldToDispose.Add(phys.Value);
				}

				ecb.RemoveComponent<PhysicsCollider>(entity);
				ecb.RemoveComponent<PhysicsWorldIndex>(entity);
				ecb.RemoveComponent<HasCollider>(entity);
			}

			entityArray.Dispose();
			positionArray.Dispose();
		}

		[BurstCompile]
		private NativeList<BlobAssetReference<Collider>> CompleteCurrentJobs(
			ref SystemState state, EntityCommandBuffer ecb, ref NativeHashSet<Entity> justBaked,
			ref NativeList<BlobAssetReference<Collider>> oldToDispose)
		{
			if (!isJobActive) return oldToDispose;

			// Apply only once the batch has finished on its own. Urgent chunks used to
			// force-complete here, but the batch's dependency chain reaches back through
			// populate/tile batch handles — a 100ms+ main-thread stall, repeated by
			// fixed-step catch-up. Waiting one more tick is always cheaper than that.
			if (!activeJobHandle.IsCompleted) return oldToDispose;

			for (var i = 0; i < activeEntities.Length; i++)
				justBaked.Add(activeEntities[i]);
			activeJobHandle.Complete(); // already finished — releases the fence, no stall
			ApplyBatch(ref state, ecb, ref oldToDispose);
			isJobActive = false;

			return oldToDispose;
		}

		[BurstCompile]
		private void ScheduleBatch(ref SystemState state)
		{
			activeBlobs =
				new NativeArray<BlobAssetReference<Collider>>(activeEntities.Length, Allocator.Persistent,
				                                              NativeArrayOptions.UninitializedMemory);
			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;

			var handlesToWait = new NativeList<JobHandle>(activeEntities.Length * 7, Allocator.Temp);
			for (var i = 0; i < activeEntities.Length; i++)
			{
				if (state.EntityManager.HasComponent<ChunkActiveJob>(activeEntities[i]))
					handlesToWait.Add(state.EntityManager.GetComponentData<ChunkActiveJob>(activeEntities[i]).Handle);

				int3 p = activePositions[i];
				for (var f = 0; f < 6; f++)
				{
					int3 np = p + faceChecks[f];
					if (!chunkMap.TryGetValue(np, out Entity ne)) continue;
					if (state.EntityManager.HasComponent<ChunkActiveJob>(ne))
						handlesToWait.Add(state.EntityManager.GetComponentData<ChunkActiveJob>(ne).Handle);
				}
			}

			JobHandle dependencies = handlesToWait.Length > 0
				                         ? JobHandle.CombineDependencies(handlesToWait.AsArray())
				                         : default;
			handlesToWait.Dispose();

			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			var map      = SystemAPI.GetSingleton<ChunkMapSingleton>();

			var job = new ChunkColliderMeshJob
			          {
				          Entities        = activeEntities,
				          Positions       = activePositions,
				          BlockDataLookup = map.ChunkDataLookup,
				          ChunkMap        = map.ChunkMap,
				          BlockPrototypes = registry.Blocks,
				          MeshDatas = registry.Meshes,
				          ChunkSize       = ChunkData.CHUNK_SIZE,
				          Filter          = chunkFilter,
				          OutColliders    = activeBlobs
			          };

			activeJobHandle = job.ScheduleParallelByRef(activeEntities.Length, 1, dependencies);
			isJobActive     = true;
		}

		[BurstCompile]
		private void ApplyBatch(
			ref SystemState                              state,
			EntityCommandBuffer                          ecb,
			ref NativeList<BlobAssetReference<Collider>> oldToDispose)
		{
			for (var i = 0; i < activeEntities.Length; i++)
			{
				Entity                       activeEntity = activeEntities[i];
				BlobAssetReference<Collider> blob         = activeBlobs[i];

				if (!state.EntityManager.Exists(activeEntity))
				{
					if (blob.IsCreated) blob.Dispose();
					continue;
				}

				ecb.TryRemoveComponent<NeedsColliderSync>(ref state, activeEntity);
				ecb.TryRemoveComponent<UrgentColliderSync>(ref state, activeEntity);

				if (blob.IsCreated)
				{
					var chunkWorldPos = new float3(activePositions[i] * ChunkData.CHUNK_SIZE);
					ecb.TryAddComponent(ref state, activeEntity, LocalTransform.FromPosition(chunkWorldPos));
					ecb.TryAddComponent<LocalToWorld>(ref state, activeEntity);

					if (state.EntityManager.HasComponent<PhysicsCollider>(activeEntity))
					{
						var old = state.EntityManager.GetComponentData<PhysicsCollider>(activeEntity);
						if (old.Value.IsCreated) oldToDispose.Add(old.Value);
						ecb.SetComponent(activeEntity, new PhysicsCollider { Value = blob });
					}
					else
					{
						ecb.AddComponent(activeEntity, new PhysicsCollider { Value         = blob });
						ecb.AddSharedComponent(activeEntity, new PhysicsWorldIndex { Value = 0 });
						ecb.AddComponent<HasCollider>(activeEntity);
					}
				}
				else if (state.EntityManager.HasComponent<PhysicsCollider>(activeEntity))
				{
					var old = state.EntityManager.GetComponentData<PhysicsCollider>(activeEntity);
					if (old.Value.IsCreated) oldToDispose.Add(old.Value);
					ecb.RemoveComponent<PhysicsCollider>(activeEntity);
					ecb.RemoveComponent<HasCollider>(activeEntity);
				}
			}

			activeEntities.Dispose();
			activePositions.Dispose();
			activeBlobs.Dispose();
		}

		[BurstCompile]
		private static bool IsChebyshevNear(in int3 a, in int3 b, int radius)
		{
			int3 d = math.abs(a - b);
			return d.x <= radius && d.y <= radius && d.z <= radius;
		}
	}
}