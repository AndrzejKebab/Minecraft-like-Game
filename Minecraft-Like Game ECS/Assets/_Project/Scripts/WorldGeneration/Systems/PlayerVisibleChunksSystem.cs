using System.Collections.Generic;
using _Project.Character;
using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
	[UpdateBefore(typeof(ChunkPopulateSystem))]
	[UpdateBefore(typeof(PlayerInteractionSystem))]
	public partial struct PlayerVisibleChunksSystem : ISystem
	{
		private int3             lastPlayerChunk;
		private NativeList<int3> pendingCreate; // coords that still need an entity, drained per frame

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();

			var diameter = (GameSettings.ViewDistanceInChunks + 2) * 2 + 1;
			var capacity = diameter * diameter * diameter;

			Entity mapEntity = state.EntityManager.CreateEntity();
			state.EntityManager.SetName(mapEntity, "ChunkMapSingleton");
			state.EntityManager.AddComponentData(mapEntity, new ChunkMapSingleton
			                                                {
				                                                ChunkMap =
					                                                new NativeHashMap<int3, Entity>(capacity,
					                                                 Allocator.Persistent),
				                                                ChunkDataLookup =
					                                                new NativeHashMap<Entity, ChunkComponent>(capacity,
					                                                 Allocator.Persistent)
			                                                });

			lastPlayerChunk = new int3(int.MaxValue);
			pendingCreate   = new NativeList<int3>(4096, Allocator.Persistent);
		}

		public void OnDestroy(ref SystemState state)
		{
			if (pendingCreate.IsCreated) pendingCreate.Dispose();

			EntityQuery q = state.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ChunkMapSingleton>());
			if (!q.IsEmpty)
			{
				var s = q.GetSingleton<ChunkMapSingleton>();
				if (s.ChunkDataLookup.IsCreated)
				{
					// dispose every chunk's BlockData — without this, play-mode exit
					// leaks tens of thousands of persistent allocations
					foreach (KVPair<Entity, ChunkComponent> kv in s.ChunkDataLookup)
						if (kv.Value.BlockData.IsCreated)
							kv.Value.BlockData.Dispose();
					s.ChunkDataLookup.Dispose();
				}

				if (s.ChunkMap.IsCreated) s.ChunkMap.Dispose();
			}

			q.Dispose();
		}

		public void OnUpdate(ref SystemState state)
		{
			Entity playerEntity = SystemAPI.GetSingletonEntity<Player>();
			float3 playerPos    = SystemAPI.GetComponentRO<LocalTransform>(playerEntity).ValueRO.Position;
			int3   playerChunk  = Utility.WorldToChunkCoord(playerPos);

			// ChunkMapSingleton's hashmaps are pointer-backed, so a by-value copy still
			// mutates the shared maps — no ref needed, and nothing to invalidate across
			// the structural changes we make below.
			var           map = SystemAPI.GetSingleton<ChunkMapSingleton>();
			EntityManager em  = state.EntityManager;

			// Reachability only changes when the player crosses into a new chunk — only
			// then do we re-diff the desired set. Creation itself is budgeted every frame.
			if (!playerChunk.Equals(lastPlayerChunk))
			{
				lastPlayerChunk = playerChunk;
				Rediff(em, map, playerChunk);
			}

			DrainPendingCreates(em, map, playerChunk);
		}

		/// <summary>
		///     On a boundary crossing: mark chunks that fell out of range for destruction,
		///     revive any in-range chunk that was pending destruction (so re-entering an
		///     area never duplicates it), and queue the newly-in-range chunks for creation.
		///     All structural changes are batched into one ECB playback (one sync point).
		/// </summary>
		private void Rediff(EntityManager em, ChunkMapSingleton map, int3 playerChunk)
		{
			int viewDist     = GameSettings.ViewDistanceInChunks;
			int populateDist = viewDist + 1;

			var ecb = new EntityCommandBuffer(Allocator.Temp);

			// mark loaded chunks now outside the populate box (kept in the map until the
			// ChunkManager actually destroys them, so they can be revived on the way back)
			foreach (KVPair<int3, Entity> kvp in map.ChunkMap)
			{
				int3 d = math.abs(kvp.Key - playerChunk);
				if (math.cmax(d) > populateDist && !em.HasComponent<MarkedToDestroy>(kvp.Value))
				{
					ecb.AddComponent<MarkedToDestroy>(kvp.Value);
					ecb.RemoveComponent<IsInViewRange>(kvp.Value); // stops collider baking on it
				}
			}

			// scan the box: revive/refresh existing chunks, queue missing ones for creation
			pendingCreate.Clear();
			for (var y = -populateDist; y <= populateDist; y++)
			for (var x = -populateDist; x <= populateDist; x++)
			for (var z = -populateDist; z <= populateDist; z++)
			{
				int3 coord    = playerChunk + new int3(x, y, z);
				var  isRender = math.abs(x) < viewDist && math.abs(y) < viewDist && math.abs(z) < viewDist;

				if (map.ChunkMap.TryGetValue(coord, out Entity e))
				{
					if (em.HasComponent<MarkedToDestroy>(e)) // revive a chunk on its way out
					{
						ecb.RemoveComponent<MarkedToDestroy>(e);
						ecb.AddComponent<IsInViewRange>(e);
					}

					if (isRender && !em.HasComponent<NeedsRender>(e)) ecb.AddComponent<NeedsRender>(e);
				}
				else
				{
					pendingCreate.Add(coord);
				}
			}

			// Drain pops from the tail, so order farthest-first: the nearest missing chunk
			// is created first and the player's own surroundings fill outward. Without this
			// the raw y,x,z scan order leaves the far corner at the tail and the player
			// spawns into an empty hole while distant chunks generate first.
			pendingCreate.Sort(new FarthestFirst { Player = playerChunk });

			ecb.Playback(em);
			ecb.Dispose();
		}

		/// <summary> Orders chunk coords by squared distance to the player, farthest first. </summary>
		private struct FarthestFirst : IComparer<int3>
		{
			public int3 Player;

			public int Compare(int3 a, int3 b)
			{
				int3 da = a - Player;
				int3 db = b - Player;
				int  sa = da.x * da.x + da.y * da.y + da.z * da.z;
				int  sb = db.x * db.x + db.y * db.y + db.z * db.z;
				return sb.CompareTo(sa); // descending — nearest ends up at the tail
			}
		}

		/// <summary>
		///     Create up to CHUNK_CREATES_PER_FRAME queued chunks this frame. Spreading the
		///     shell over frames turns the boundary-crossing hitch (a shell of ~1000 chunks,
		///     each a 128 KB alloc + several structural changes) into a smooth trickle.
		/// </summary>
		private void DrainPendingCreates(EntityManager em, ChunkMapSingleton map, int3 playerChunk)
		{
			int viewDist = GameSettings.ViewDistanceInChunks;
			var made     = 0;

			while (pendingCreate.Length > 0 && made < GameSettings.CHUNK_CREATES_PER_FRAME)
			{
				int3 coord = pendingCreate[pendingCreate.Length - 1];
				pendingCreate.RemoveAt(pendingCreate.Length - 1);

				if (map.ChunkMap.ContainsKey(coord)) continue; // already created or revived
				var isRender = math.cmax(math.abs(coord - playerChunk)) < viewDist;

				Entity entity = em.CreateEntity();
				em.AddComponentData(entity, new ChunkPositionComponent { ChunkCoord = coord });

				var chunkComp = new ChunkComponent
				                {
					                BlockData = new NativeArray<BlockState>(
					                                                        ChunkData.CHUNK_SIZE * ChunkData.CHUNK_SIZE *
					                                                        ChunkData.CHUNK_SIZE,
					                                                        Allocator.Persistent,
					                                                        NativeArrayOptions.UninitializedMemory)
				                };

				em.AddComponentData(entity, chunkComp);
				map.ChunkDataLookup.Add(entity, chunkComp);

				em.AddComponentData(entity, new ChunkActiveJob { Handle = default });
				em.AddComponentData(entity, new IsInViewRange());
				em.AddComponentData(entity, LocalTransform.FromPosition(new float3(
				                                                         coord.x * ChunkData.CHUNK_SIZE,
				                                                         coord.y * ChunkData.CHUNK_SIZE,
				                                                         coord.z * ChunkData.CHUNK_SIZE)));

				if (isRender) em.AddComponentData(entity, new NeedsRender());

				em.AddComponentData(entity, new NeedsPopulation());

				map.ChunkMap.Add(coord, entity);
				made++;
			}
		}
	}
}