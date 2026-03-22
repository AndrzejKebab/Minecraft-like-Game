using _Project.Tags;
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
	public partial struct PlayerVisibleChunksSystem : ISystem
	{
		private int3 lastPlayerChunk;

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
					                                                 Allocator.Persistent)
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
			var populateDist = viewDist + 1;
			var diameter     = populateDist * 2 + 1;

			EntityManager em = state.EntityManager;

			// ── 1. Build desired coord set ─────────────────────────────────────
			var desired = new NativeHashMap<int3, bool>(diameter * diameter * diameter, Allocator.Temp);

			for (var y = -populateDist; y <= populateDist; y++)
			for (var x = -populateDist; x <= populateDist; x++)
			for (var z = -populateDist; z <= populateDist; z++)
			{
				int3 c        = playerChunk + new int3(x, y, z);
				var  isRender = math.abs(x) < viewDist && math.abs(y) < viewDist && math.abs(z) < viewDist;
				desired.TryAdd(c, isRender);
			}

			// ── 2. Destroy chunks that left view ───────────────────────────────
			var toRemove = new NativeList<int3>(64, Allocator.Temp);
			foreach (KVPair<int3, Entity> kvp in mapSingleton.ChunkMap)
				if (!desired.ContainsKey(kvp.Key))
					toRemove.Add(kvp.Key);

			if (toRemove.Length > 0)
			{
				var popSystem  = state.World.GetExistingSystemManaged<ChunkPopulateSystem>();
				var meshSystem = state.World.GetExistingSystemManaged<ChunkMeshBuilderSystem>();

				popSystem?.CompleteAllJobs();
				meshSystem?.CompleteAllJobs();
			}

			foreach (int3 coord in toRemove)
			{
				Entity entity = mapSingleton.ChunkMap[coord];

				if (em.HasComponent<ChunkComponent>(entity))
				{
					var comp = em.GetComponentData<ChunkComponent>(entity);
					if (comp.BlockData.IsCreated) comp.BlockData.Dispose();
				}

				if (em.HasComponent<ChunkMeshData>(entity))
					em.GetComponentData<ChunkMeshData>(entity).Dispose();

				em.DestroyEntity(entity);
				mapSingleton.ChunkMap.Remove(coord);
			}

			// ── 3. Create entities for new chunks OR upgrade existing ones ─────
			foreach (KVPair<int3, bool> kvp in desired)
			{
				int3 coord    = kvp.Key;
				var  isRender = kvp.Value;
				var  dist     = math.distance(playerChunk, coord);

				if (mapSingleton.ChunkMap.TryGetValue(coord, out Entity existingEntity))
				{
					em.SetComponentData(existingEntity, new ChunkPriorityComponent { Distance = dist });

					if (isRender && !em.HasComponent<NeedsRender>(existingEntity) &&
					    !em.HasComponent<HasRenderMesh>(existingEntity))
						em.AddComponentData(existingEntity, new NeedsRender());
					continue;
				}

				Entity entity = em.CreateEntity();

				em.AddComponentData(entity, new ChunkPositionComponent { ChunkCoord = coord });
				em.AddComponentData(entity, new ChunkComponent { BlockData          = default });
				em.AddComponentData(entity, new IsVisible());
				em.AddComponentData(entity, new ChunkPriorityComponent { Distance = dist });
				em.AddComponentData(entity, LocalTransform.FromPosition(new float3(
				                                                         coord.x * VoxelData.CHUNK_SIZE,
				                                                         coord.y * VoxelData.CHUNK_SIZE,
				                                                         coord.z * VoxelData.CHUNK_SIZE)));

				if (isRender)
					em.AddComponentData(entity, new NeedsRender());

				mapSingleton.ChunkMap.Add(coord, entity);
			}

			toRemove.Dispose();
			desired.Dispose();
		}

		public static int3 WorldToChunkCoord(float3 worldPos)
		{
			return new int3(
			                Mathf.FloorToInt(worldPos.x / VoxelData.CHUNK_SIZE),
			                Mathf.FloorToInt(worldPos.y / VoxelData.CHUNK_SIZE),
			                Mathf.FloorToInt(worldPos.z / VoxelData.CHUNK_SIZE));
		}
	}
}