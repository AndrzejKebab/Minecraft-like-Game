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
		private int3 lastPlayerChunk;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();

			int diameter = (GameSettings.ViewDistanceInChunks + 2) * 2 + 1;
			int capacity = diameter * diameter * diameter;

			Entity mapEntity = state.EntityManager.CreateEntity();
			state.EntityManager.SetName(mapEntity, "ChunkMapSingleton");
			state.EntityManager.AddComponentData(mapEntity, new ChunkMapSingleton
			{
				ChunkMap        = new NativeHashMap<int3, Entity>(capacity, Allocator.Persistent),
				ChunkDataLookup = new NativeHashMap<Entity, ChunkComponent>(capacity, Allocator.Persistent)
			});

			lastPlayerChunk = new int3(int.MaxValue);
		}

		public void OnDestroy(ref SystemState state)
		{
			EntityQuery q = state.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ChunkMapSingleton>());
			if (!q.IsEmpty)
			{
				var s = q.GetSingleton<ChunkMapSingleton>();
				if (s.ChunkMap.IsCreated) s.ChunkMap.Dispose();
				if (s.ChunkDataLookup.IsCreated) s.ChunkDataLookup.Dispose();
			}
			q.Dispose();
		}

		public void OnUpdate(ref SystemState state)
		{
			Entity playerEntity = SystemAPI.GetSingletonEntity<Player>();
			float3 playerPos    = SystemAPI.GetComponentRO<LocalTransform>(playerEntity).ValueRO.Position;

			int3 playerChunk = WorldToChunkCoord(playerPos);
			if (playerChunk.Equals(lastPlayerChunk)) return;
			lastPlayerChunk = playerChunk;

			ref ChunkMapSingleton mapSingleton = ref SystemAPI.GetSingletonRW<ChunkMapSingleton>().ValueRW;

			int viewDist     = GameSettings.ViewDistanceInChunks;
			int populateDist = viewDist + 1;
			int diameter     = populateDist * 2 + 1;

			EntityManager em = state.EntityManager;

			var desired = new NativeHashMap<int3, bool>(diameter * diameter * diameter, Allocator.Temp);

			for (int y = -populateDist; y <= populateDist; y++)
			for (int x = -populateDist; x <= populateDist; x++)
			for (int z = -populateDist; z <= populateDist; z++)
			{
				int3 c        = playerChunk + new int3(x, y, z);
				bool isRender = math.abs(x) < viewDist && math.abs(y) < viewDist && math.abs(z) < viewDist;
				desired.TryAdd(c, isRender);
			}

			var toRemove = new NativeList<int3>(64, Allocator.Temp);
			foreach (KVPair<int3, Entity> kvp in mapSingleton.ChunkMap)
				if (!desired.ContainsKey(kvp.Key)) toRemove.Add(kvp.Key);

			foreach (int3 coord in toRemove)
			{
				Entity entity = mapSingleton.ChunkMap[coord];
				em.AddComponentData(entity, new MarkedToDestroy());
				em.RemoveComponent<IsInViewRange>(entity);
				if (em.HasComponent<NeedsRender>(entity)) em.RemoveComponent<NeedsRender>(entity);
				mapSingleton.ChunkMap.Remove(coord);
			}

			foreach (KVPair<int3, bool> kvp in desired)
			{
				int3  coord    = kvp.Key;
				bool  isRender = kvp.Value;
				float dist     = math.distance(playerChunk, coord);

				if (mapSingleton.ChunkMap.TryGetValue(coord, out Entity existingEntity))
				{
					em.SetComponentData(existingEntity, new ChunkPriorityComponent { Distance = dist, Importance = 1 });
					if (isRender && !em.HasComponent<NeedsRender>(existingEntity))
						em.AddComponentData(existingEntity, new NeedsRender());
					continue;
				}

				Entity entity = em.CreateEntity();
				em.SetName(entity, "Chunk");
				em.AddComponentData(entity, new ChunkPositionComponent { ChunkCoord = coord });

				var chunkComp = new ChunkComponent
				{
					BlockData = new NativeArray<BlockState>(
						VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE,
						Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
				};

				em.AddComponentData(entity, chunkComp);
				mapSingleton.ChunkDataLookup.Add(entity, chunkComp);

				em.AddComponentData(entity, new ChunkActiveJob { Handle = default });
				em.AddComponentData(entity, new IsInViewRange());
				em.AddComponentData(entity, new ChunkPriorityComponent { Distance = dist, Importance = 1 });
				em.AddComponentData(entity, LocalTransform.FromPosition(new float3(
					coord.x * VoxelData.CHUNK_SIZE,
					coord.y * VoxelData.CHUNK_SIZE,
					coord.z * VoxelData.CHUNK_SIZE)));

				if (isRender) em.AddComponentData(entity, new NeedsRender());

				// Single tag, replaces NeedsTerrainTag/NeedsDecorationTag.
				em.AddComponentData(entity, new NeedsPopulation());

				mapSingleton.ChunkMap.Add(coord, entity);
			}

			toRemove.Dispose();
			desired.Dispose();
		}

		public static int3 WorldToChunkCoord(float3 worldPos) => new int3(
			Mathf.FloorToInt(worldPos.x / VoxelData.CHUNK_SIZE),
			Mathf.FloorToInt(worldPos.y / VoxelData.CHUNK_SIZE),
			Mathf.FloorToInt(worldPos.z / VoxelData.CHUNK_SIZE));
	}
}