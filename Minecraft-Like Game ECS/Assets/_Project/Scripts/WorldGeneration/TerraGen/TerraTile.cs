using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Tile layout constants. A tile covers TILE_CHUNKS × TILE_CHUNKS chunk columns
	///     and is generated with a BORDER of extra columns on every side (the RTF tile
	///     border) so the erosion/smoothing filters have context and every chunk in the
	///     tile can read a full 1-chunk halo (for decoration) without touching
	///     neighbouring tiles.
	/// </summary>
	public static class TerraTileConst
	{
		public const int TILE_SHIFT  = 2;              // 4 chunks per tile axis
		public const int TILE_CHUNKS = 1 << TILE_SHIFT;
		public const int TILE_BLOCKS = TILE_CHUNKS * ChunkData.CHUNK_SIZE;
		public const int BORDER      = ChunkData.CHUNK_SIZE; // 32 — one full chunk halo
		public const int GEN_BLOCKS  = TILE_BLOCKS + BORDER * 2;

		/// <summary> Tile coord of a chunk column (arithmetic shift floors negatives). </summary>
		public static int2 TileOfChunk(int2 chunkXZ)
		{
			return new int2(chunkXZ.x >> TILE_SHIFT, chunkXZ.y >> TILE_SHIFT);
		}

		/// <summary> World-space block origin of a tile's bordered grid. </summary>
		public static int2 GenOrigin(int2 tileCoord)
		{
			return new int2(tileCoord.x * TILE_BLOCKS - BORDER,
			                tileCoord.y * TILE_BLOCKS - BORDER);
		}
	}

	/// <summary>
	///     One cached tile: the full bordered grid of generated columns
	///     (GEN_BLOCKS × GEN_BLOCKS, Persistent) plus the job handles guarding it.
	///     GenHandle = the producer (tile generation job); ReadHandle = combined
	///     consumers (chunk populate jobs). Only TerraTileSystem disposes tiles,
	///     and only after both handles completed.
	/// </summary>
	public struct TerraTile
	{
		public NativeArray<TerraColumn> Columns;
		public JobHandle                GenHandle;
		public JobHandle                ReadHandle;
	}

	/// <summary>
	///     Tile cache singleton. Mutated only on the main thread by TerraTileSystem
	///     (before ChunkPopulateSystem runs) and read only on the main thread by
	///     ChunkPopulateSystem — jobs never touch the map itself, they get per-batch
	///     TerraTileSlice arrays instead.
	/// </summary>
	public struct TerraTileCacheSingleton : IComponentData
	{
		public NativeHashMap<int2, TerraTile> Tiles;
	}

	/// <summary>
	///     Per-chunk view into a cached tile, resolved on the main thread when the
	///     populate batch is built. Columns aliases the tile's persistent array.
	/// </summary>
	public struct TerraTileSlice
	{
		public NativeArray<TerraColumn> Columns; // GEN_BLOCKS² bordered grid
		public int                      OriginX; // world block coords of grid [0,0]
		public int                      OriginZ;
	}

	/// <summary>
	///     Intermediate per-column cell used during tile generation — the subset of
	///     TerraCell the erosion/smoothing filters and column quantisation need.
	/// </summary>
	public struct TerraGenCell
	{
		public float        Height;          // normalised, mutated by filters
		public float        RiverMask;
		public float        RiverWaterLevel; // normalised, 0 = none
		public float        RegionEdge;      // terrain region edge (erosion modifier)
		public TerraTerrain Terrain;
		public TerraBiome   Biome;
	}
}
