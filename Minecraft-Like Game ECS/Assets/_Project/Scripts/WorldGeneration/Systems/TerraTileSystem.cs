using System;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.TerraGen;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
	/// <summary>
	///     Owns the TerraGen tile cache (the port of RTF's TileGenerator + TileCache).
	///     Each tile (4×4 chunk columns + border) is generated once by a Burst job and
	///     shared by every chunk — including all vertical chunks of a column — instead
	///     of every chunk regenerating its own halo.
	///     Runs before ChunkPopulateSystem: schedules generation for tiles needed by
	///     chunks awaiting population (nearest first, budgeted per frame) and evicts
	///     tiles outside the keep radius once no jobs use them.
	///     The map is only ever touched on the main thread; jobs receive per-batch
	///     TerraTileSlice views and are guarded by the tile's Gen/Read handles.
	/// </summary>
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(PlayerVisibleChunksSystem))]
	[UpdateBefore(typeof(ChunkPopulateSystem))]
	public partial struct TerraTileSystem : ISystem
	{
		// tiles generated per frame — all dispatched together in one parallel job
		private const int MAX_TILES_PER_FRAME = 8;

		private EntityQuery needQuery;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<TerraTileCacheSingleton>();
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<TerraGenSettings>();

			needQuery = SystemAPI.QueryBuilder()
			                     .WithAll<NeedsPopulation, ChunkPositionComponent>()
			                     .WithNone<MarkedToDestroy>()
			                     .Build();

			var cache = new TerraTileCacheSingleton
			            {
				            Tiles = new NativeHashMap<int2, TerraTile>(512, Allocator.Persistent)
			            };
			state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), cache);
		}

		public void OnDestroy(ref SystemState state)
		{
			if (!SystemAPI.TryGetSingleton(out TerraTileCacheSingleton cache)) return;

			foreach (KVPair<int2, TerraTile> kv in cache.Tiles)
			{
				kv.Value.GenHandle.Complete();
				kv.Value.ReadHandle.Complete();
				kv.Value.Columns.Dispose();
			}

			cache.Tiles.Dispose();
		}

		public void OnUpdate(ref SystemState state)
		{
			var settings = SystemAPI.GetSingleton<TerraGenSettings>();
			var cache    = SystemAPI.GetSingleton<TerraTileCacheSingleton>();

			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
				                   SystemAPI.GetSingletonEntity<Player>()).ValueRO.Position;
			int3 playerChunk = Utility.WorldToChunkCoord(playerPos);
			int2 playerTile  = TerraTileConst.TileOfChunk(new int2(playerChunk.x, playerChunk.z));

			ScheduleNeededTiles(ref state, cache, settings, playerTile);
			EvictFarTiles(cache, playerTile);
		}

		private void ScheduleNeededTiles(ref SystemState state, TerraTileCacheSingleton cache,
		                                 TerraGenSettings settings, int2 playerTile)
		{
			if (needQuery.IsEmpty) return;

			NativeArray<ChunkPositionComponent> positions =
				needQuery.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			// unique missing tile coords, nearest first
			var missing = new NativeList<TileCandidate>(16, Allocator.Temp);
			var seen    = new NativeHashSet<int2>(64, Allocator.Temp);
			for (var i = 0; i < positions.Length; i++)
			{
				int2 tile = TerraTileConst.TileOfChunk(new int2(positions[i].ChunkCoord.x,
				                                                positions[i].ChunkCoord.z));
				if (!seen.Add(tile) || cache.Tiles.ContainsKey(tile)) continue;

				int2 d = tile - playerTile;
				missing.Add(new TileCandidate { Coord = tile, DistSq = d.x * d.x + d.y * d.y });
			}

			positions.Dispose();
			seen.Dispose();

			if (missing.Length == 0)
			{
				missing.Dispose();
				return;
			}

			missing.Sort();

			// take the nearest N missing tiles and generate them all in ONE parallel
			// dispatch (one worker-thread slice per tile) instead of N separate jobs
			var take = math.min(missing.Length, MAX_TILES_PER_FRAME);
			var coords  = new NativeArray<int2>(take, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var ptrs    = new NativeArray<IntPtr>(take, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var colArrs   = new NativeArray<TerraColumn>[take];
			var coordVals = new int2[take]; // managed copy so we don't touch the arrays post-schedule

			for (var i = 0; i < take; i++)
			{
				int2 coord = missing[i].Coord;
				var columns = new NativeArray<TerraColumn>(
					TerraTileConst.GEN_BLOCKS * TerraTileConst.GEN_BLOCKS,
					Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
				colArrs[i]   = columns;
				coordVals[i] = coord;
				coords[i]    = coord;
				unsafe { ptrs[i] = (IntPtr)columns.GetUnsafePtr(); }
			}

			missing.Dispose();

			JobHandle handle = new TerraTileGenJob
			                   {
				                   Settings   = settings,
				                   TileCoords = coords,
				                   ColumnPtrs = ptrs
			                   }.Schedule(take, 1);

			for (var i = 0; i < take; i++)
				cache.Tiles.Add(coordVals[i], new TerraTile
				                              {
					                              Columns    = colArrs[i],
					                              GenHandle  = handle,
					                              ReadHandle = default
				                              });

			coords.Dispose(handle);
			ptrs.Dispose(handle);

			JobHandle.ScheduleBatchedJobs();
		}

		private void EvictFarTiles(TerraTileCacheSingleton cache, int2 playerTile)
		{
			// chunks despawn at ViewDistanceInChunks; keep a margin so tiles outlive them
			var keepRadius = GameSettings.ViewDistanceInChunks / TerraTileConst.TILE_CHUNKS + 2;

			var toRemove = new NativeList<int2>(8, Allocator.Temp);
			foreach (KVPair<int2, TerraTile> kv in cache.Tiles)
			{
				int2 d = kv.Key - playerTile;
				if (math.max(math.abs(d.x), math.abs(d.y)) <= keepRadius) continue;
				// defer eviction until nothing is producing or consuming the tile
				if (!kv.Value.GenHandle.IsCompleted || !kv.Value.ReadHandle.IsCompleted) continue;
				toRemove.Add(kv.Key);
			}

			for (var i = 0; i < toRemove.Length; i++)
			{
				TerraTile tile = cache.Tiles[toRemove[i]];
				tile.GenHandle.Complete();
				tile.ReadHandle.Complete();
				tile.Columns.Dispose();
				cache.Tiles.Remove(toRemove[i]);
			}

			toRemove.Dispose();
		}

		private struct TileCandidate : IComparable<TileCandidate>
		{
			public int2 Coord;
			public int  DistSq;

			public int CompareTo(TileCandidate other)
			{
				return DistSq.CompareTo(other.DistSq);
			}
		}
	}
}
