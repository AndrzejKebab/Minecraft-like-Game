using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
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

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<ChunkMapSingleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();

			var  layer     = LayerMask.NameToLayer("Chunk");
			uint chunkMask = layer == -1 ? (1u << 3) : (1u << layer);
			chunkFilter = new CollisionFilter { BelongsTo = chunkMask, CollidesWith = ~0u };

			faceChecks = new NativeArray<int3>(6, Allocator.Persistent)
			             {
				             [0] = new int3(-1, 0, 0), [1] = new int3(1, 0, 0),
				             [2] = new int3(0, -1, 0), [3] = new int3(0, 1, 0),
				             [4] = new int3(0, 0, -1), [5] = new int3(0, 0, 1)
			             };
		}

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

		public void OnUpdate(ref SystemState state)
		{
			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
				SystemAPI.GetSingletonEntity<Player>()).ValueRO.Position;
			int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);

			var ecb          = new EntityCommandBuffer(Allocator.Temp);
			var oldToDispose = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);

			// ── 1. Complete active job if ready or urgent ──────────────────────
			if (isJobActive)
			{
				bool forceComplete = activeJobHandle.IsCompleted;

				if (!forceComplete)
					for (var i = 0; i < activeEntities.Length; i++)
					{
						if (!state.EntityManager.Exists(activeEntities[i]) ||
						    !state.EntityManager.HasComponent<UrgentColliderSync>(activeEntities[i])) continue;
						forceComplete = true;
						break;
					}

				if (!forceComplete)
					foreach (RefRO<UrgentColliderSync> _ in SystemAPI.Query<RefRO<UrgentColliderSync>>())
					{
						forceComplete = true;
						break;
					}

				if (forceComplete)
				{
					activeJobHandle.Complete();
					ApplyBatch(ref state, ecb, oldToDispose);
					isJobActive = false;
				}
			}

			// ── 2. Strip colliders for chunks that left radius ─────────────────
			foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in
			         SystemAPI.Query<RefRO<ChunkPositionComponent>>()
			                  .WithAll<HasCollider>()
			                  .WithEntityAccess())
			{
				if (IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;

				if (state.EntityManager.HasComponent<PhysicsCollider>(entity))
				{
					var phys = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
					if (phys.Value.IsCreated) oldToDispose.Add(phys.Value);
				}

				ecb.RemoveComponent<PhysicsCollider>(entity);
				ecb.RemoveComponent<PhysicsWorldIndex>(entity);
				ecb.RemoveComponent<HasCollider>(entity);
			}

			// ── 3. Collect candidates and schedule new batch when idle ─────────
			if (!isJobActive)
			{
				var candEntities  = new NativeList<Entity>(64, Allocator.Temp);
				var candPositions = new NativeList<int3>(64, Allocator.Temp);
				var hasUrgent     = false;

				foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in
				         SystemAPI.Query<RefRO<ChunkPositionComponent>>()
				                  .WithAll<IsPopulated, IsInViewRange>()
				                  .WithEntityAccess())
				{
					if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;

					var hasCollider = state.EntityManager.HasComponent<HasCollider>(entity);
					var isUrgent    = state.EntityManager.HasComponent<UrgentColliderSync>(entity);
					var needsRebake = state.EntityManager.HasComponent<NeedsColliderSync>(entity) || isUrgent;

					if (hasCollider && !needsRebake) continue;

					candEntities.Add(entity);
					candPositions.Add(pos.ValueRO.ChunkCoord);
					if (isUrgent) hasUrgent = true;
				}

				if (candEntities.Length > 0)
				{
					ScheduleBatch(ref state, candEntities.AsArray(), candPositions.AsArray());

					if (hasUrgent)
					{
						activeJobHandle.Complete();
						ApplyBatch(ref state, ecb, oldToDispose);
						isJobActive = false;
					}
				}

				candEntities.Dispose();
				candPositions.Dispose();
			}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();

			foreach (BlobAssetReference<Collider> c in oldToDispose) c.Dispose();
			oldToDispose.Dispose();
		}

		private void ScheduleBatch(
			ref SystemState     state,
			NativeArray<Entity> entities,
			NativeArray<int3>   positions)
		{
			activeEntities  = new NativeArray<Entity>(entities, Allocator.Persistent);
			activePositions = new NativeArray<int3>(positions, Allocator.Persistent);
			activeBlobs =
				new NativeArray<BlobAssetReference<Collider>>(entities.Length, Allocator.Persistent,
				                                              NativeArrayOptions.UninitializedMemory);
			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;

			var handlesToWait = new NativeList<JobHandle>(entities.Length * 7, Allocator.Temp);
			for (var i = 0; i < entities.Length; i++)
			{
				if (state.EntityManager.HasComponent<ChunkActiveJob>(entities[i]))
					handlesToWait.Add(state.EntityManager.GetComponentData<ChunkActiveJob>(entities[i]).Handle);

				int3 p = positions[i];
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
				          ChunkSize       = VoxelData.CHUNK_SIZE,
				          Filter          = chunkFilter,
				          OutColliders    = activeBlobs
			          };

			activeJobHandle = job.ScheduleParallelByRef(entities.Length, 1, dependencies);
			isJobActive     = true;
		}

		private void ApplyBatch(
			ref SystemState                          state,
			EntityCommandBuffer                      ecb,
			NativeList<BlobAssetReference<Collider>> oldToDispose)
		{
			for (var i = 0; i < activeEntities.Length; i++)
			{
				Entity                       e    = activeEntities[i];
				BlobAssetReference<Collider> blob = activeBlobs[i];

				if (!state.EntityManager.Exists(e))
				{
					if (blob.IsCreated) blob.Dispose();
					continue;
				}

				if (state.EntityManager.HasComponent<NeedsColliderSync>(e))
					ecb.RemoveComponent<NeedsColliderSync>(e);
				if (state.EntityManager.HasComponent<UrgentColliderSync>(e))
					ecb.RemoveComponent<UrgentColliderSync>(e);

				if (blob.IsCreated)
				{
					var chunkWorldPos = new float3(activePositions[i] * VoxelData.CHUNK_SIZE);
					if (!state.EntityManager.HasComponent<LocalTransform>(e))
						ecb.AddComponent(e, LocalTransform.FromPosition(chunkWorldPos));
					if (!state.EntityManager.HasComponent<LocalToWorld>(e))
						ecb.AddComponent<LocalToWorld>(e);

					if (state.EntityManager.HasComponent<PhysicsCollider>(e))
					{
						var old = state.EntityManager.GetComponentData<PhysicsCollider>(e);
						if (old.Value.IsCreated) oldToDispose.Add(old.Value);
						ecb.SetComponent(e, new PhysicsCollider { Value = blob });
					}
					else
					{
						ecb.AddComponent(e, new PhysicsCollider { Value         = blob });
						ecb.AddSharedComponent(e, new PhysicsWorldIndex { Value = 0 });
						ecb.AddComponent<HasCollider>(e);
					}
				}
				else if (state.EntityManager.HasComponent<PhysicsCollider>(e))
				{
					var old = state.EntityManager.GetComponentData<PhysicsCollider>(e);
					if (old.Value.IsCreated) oldToDispose.Add(old.Value);
					ecb.RemoveComponent<PhysicsCollider>(e);
					ecb.RemoveComponent<HasCollider>(e);
				}
			}

			activeEntities.Dispose();
			activePositions.Dispose();
			activeBlobs.Dispose();
		}

		private static bool IsChebyshevNear(in int3 a, in int3 b, int radius)
		{
			int3 d = math.abs(a - b);
			return d.x <= radius && d.y <= radius && d.z <= radius;
		}
	}
}
