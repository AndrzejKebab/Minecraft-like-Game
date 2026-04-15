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
		private const int COLLIDER_RADIUS = 1;
		private CollisionFilter   chunkFilter;
		private NativeArray<int3> faceChecks;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<ChunkMapSingleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();

			uint chunkLayer = (uint)(1 << LayerMask.NameToLayer("Chunk"));
			chunkFilter = new CollisionFilter { BelongsTo = chunkLayer, CollidesWith = ~0u };

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
		}

		public void OnUpdate(ref SystemState state)
		{
			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
				SystemAPI.GetSingletonEntity<Player>()).ValueRO.Position;
			int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);

			var ecb         = new EntityCommandBuffer(Allocator.Temp);
			var oldToDispose = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);

			// PHASE 1: Collect candidates in COLLIDER_RADIUS that need (re)bake.
			//   Trigger: in radius AND IsPopulated AND (no HasCollider OR NeedsColliderSync).
			var candEntities  = new NativeList<Entity>(64, Allocator.TempJob);
			var candPositions = new NativeList<int3>(64, Allocator.TempJob);

			foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in
			         SystemAPI.Query<RefRO<ChunkPositionComponent>>()
			                  .WithAll<IsPopulated, IsInViewRange>()
			                  .WithEntityAccess())
			{
				if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;

				bool hasCollider     = state.EntityManager.HasComponent<HasCollider>(entity);
				bool needsRebake     = state.EntityManager.HasComponent<NeedsColliderSync>(entity);
				if (hasCollider && !needsRebake) continue;

				candEntities.Add(entity);
				candPositions.Add(pos.ValueRO.ChunkCoord);
			}

			if (candEntities.Length > 0)
			{
				BakeBatch(ref state, candEntities.AsArray(), candPositions.AsArray(), ecb, oldToDispose);
			}

			candEntities.Dispose();
			candPositions.Dispose();

			// PHASE 2: Strip colliders for chunks that left radius.
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

			ecb.Playback(state.EntityManager);
			ecb.Dispose();

			foreach (BlobAssetReference<Collider> c in oldToDispose) c.Dispose();
			oldToDispose.Dispose();
		}

		private void BakeBatch(
			ref SystemState state,
			NativeArray<Entity> entities,
			NativeArray<int3>   positions,
			EntityCommandBuffer ecb,
			NativeList<BlobAssetReference<Collider>> oldToDispose)
		{
			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;

			// Complete in-flight handles for candidates + their 6 face neighbors.
			// Block data must be coherent before job reads it.
			for (int i = 0; i < entities.Length; i++)
			{
				if (state.EntityManager.HasComponent<ChunkActiveJob>(entities[i]))
					state.EntityManager.GetComponentData<ChunkActiveJob>(entities[i]).Handle.Complete();

				int3 p = positions[i];
				for (int f = 0; f < 6; f++)
				{
					int3 np = p + faceChecks[f];
					if (!chunkMap.TryGetValue(np, out Entity ne)) continue;
					if (state.EntityManager.HasComponent<ChunkActiveJob>(ne))
						state.EntityManager.GetComponentData<ChunkActiveJob>(ne).Handle.Complete();
				}
			}

			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			var map      = SystemAPI.GetSingleton<ChunkMapSingleton>();

			var outBlobs = new NativeArray<BlobAssetReference<Collider>>(
				entities.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

			var job = new ChunkColliderMeshJob
			{
				Entities        = entities,
				Positions       = positions,
				BlockDataLookup = map.ChunkDataLookup,
				ChunkMap        = map.ChunkMap,
				BlockPrototypes = registry.Blocks,
				ChunkSize       = VoxelData.CHUNK_SIZE,
				Filter          = chunkFilter,
				OutColliders    = outBlobs
			};

			JobHandle handle = job.ScheduleParallelByRef(entities.Length, 1, default);
			handle.Complete();

			// Apply results.
			for (int i = 0; i < entities.Length; i++)
			{
				Entity                       e    = entities[i];
				BlobAssetReference<Collider> blob = outBlobs[i];

				if (state.EntityManager.HasComponent<NeedsColliderSync>(e))
					ecb.RemoveComponent<NeedsColliderSync>(e);
				if (state.EntityManager.HasComponent<UrgentColliderSync>(e))
					ecb.RemoveComponent<UrgentColliderSync>(e);

				if (blob.IsCreated)
				{
					if (state.EntityManager.HasComponent<PhysicsCollider>(e))
					{
						var old = state.EntityManager.GetComponentData<PhysicsCollider>(e);
						if (old.Value.IsCreated) oldToDispose.Add(old.Value);
						ecb.SetComponent(e, new PhysicsCollider { Value = blob });
					}
					else
					{
						ecb.AddComponent(e, new PhysicsCollider { Value = blob });
						ecb.AddSharedComponent(e, new PhysicsWorldIndex { Value = 0 });
						ecb.AddComponent<HasCollider>(e);
					}
				}
				else if (state.EntityManager.HasComponent<PhysicsCollider>(e))
				{
					// Empty chunk — strip existing collider.
					var old = state.EntityManager.GetComponentData<PhysicsCollider>(e);
					if (old.Value.IsCreated) oldToDispose.Add(old.Value);
					ecb.RemoveComponent<PhysicsCollider>(e);
					ecb.RemoveComponent<HasCollider>(e);
				}
			}

			outBlobs.Dispose();
		}

		private static bool IsChebyshevNear(in int3 a, in int3 b, int radius)
		{
			int3 d = math.abs(a - b);
			return d.x <= radius && d.y <= radius && d.z <= radius;
		}
	}
}