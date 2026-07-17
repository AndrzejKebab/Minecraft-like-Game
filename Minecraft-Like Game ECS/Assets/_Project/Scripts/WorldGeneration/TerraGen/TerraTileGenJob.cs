using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Shared layout constants for the tile-generation job chain. Tile generation is
	///     split into four chained jobs so the expensive per-column noise stages spread
	///     across ALL worker threads even when only one tile is queued (a whole tile in a
	///     single Execute pinned one worker for the full ~100ms+ while the rest idled):
	///
	///     1. <see cref="TerraTileSampleJob" />   — parallel over ROWS (take × 192): terrain noise.
	///     2. <see cref="TerraTileNetworkJob" />  — parallel over TILES: river networks (cheap).
	///     3. <see cref="TerraTileCarveJob" />    — parallel over ROWS: river carve + climate noise.
	///     4. <see cref="TerraTileFilterJob" />   — parallel over TILES: erosion/smoothing/settle
	///        (droplet erosion is a sequential random walk over the whole tile — the only
	///        stage that genuinely can't split below tile granularity) + column quantise.
	///
	///     All scratch buffers are one flat allocation per batch, sliced per tile; jobs
	///     write outside their own loop index by design, hence
	///     [NativeDisableParallelForRestriction] on those arrays.
	/// </summary>
	public static class TerraTileGen
	{
		public const int SIZE  = TerraTileConst.GEN_BLOCKS;
		public const int CELLS = SIZE * SIZE;

		/// <summary> Rows per parallel batch slice (192 rows/tile ÷ 8 = 24 slices/tile). </summary>
		public const int ROW_BATCH = 8;

		/// <summary>
		///     Per-tile river-segment capacity. TerraRiverNet.BuildForCenter stops appending
		///     once the shared list reaches its MAX_SEGMENTS (2400) — across all continents —
		///     so this bound holds per tile with headroom for the final partial trace.
		/// </summary>
		public const int SEG_CAP = 2560;

		/// <summary> Max distinct continents whose networks one tile can straddle. </summary>
		public const int CENTER_CAP = 8;

		public static TerraGenCell ToGenCell(in TerraCell cell)
		{
			return new TerraGenCell
			       {
				       Height          = cell.Height,
				       RiverMask       = cell.RiverMask,
				       RiverWaterLevel = cell.RiverWaterLevel,
				       RegionEdge      = cell.TerrainRegionEdge,
				       Terrain         = cell.Terrain,
				       Biome           = cell.Biome
			       };
		}

		public static bool UseNetworks(in TerraGenSettings s)
		{
			return s.RiversEnabled && s.UseRiverNetworks;
		}
	}

	/// <summary>
	///     Stage 1 — one index per tile ROW: sample the terrain noise stack for the row's
	///     192 columns. On the network path this is SampleTerrain (climate comes later,
	///     after carving); on the simple path Sample produces the finished cell directly.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct TerraTileSampleJob : IJobParallelFor
	{
		public TerraGenSettings Settings;

		[ReadOnly] public NativeArray<int2> TileCoords;

		[NativeDisableParallelForRestriction] public NativeArray<TerraCell> Full; // take × CELLS

		public void Execute(int r)
		{
			var  tile   = r / TerraTileGen.SIZE;
			var  z      = r % TerraTileGen.SIZE;
			int2 origin = TerraTileConst.GenOrigin(TileCoords[tile]);
			var  levels = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);
			var  baseI  = tile * TerraTileGen.CELLS + z * TerraTileGen.SIZE;
			var  useNet = TerraTileGen.UseNetworks(in Settings);

			for (var x = 0; x < TerraTileGen.SIZE; x++)
			{
				TerraCell cell;
				if (useNet)
					TerraHeightmap.SampleTerrain(out cell, origin.x + x, origin.y + z,
					                             in Settings, in levels);
				else
					TerraHeightmap.Sample(out cell, origin.x + x, origin.y + z,
					                      in Settings, in levels);
				Full[baseI + x] = cell;
			}
		}
	}

	/// <summary>
	///     Stage 2 — one index per TILE: collect the continent centres present in the
	///     sampled cells, build each centre's river network once, and copy the reaches
	///     into this tile's slice of the shared segment buffer.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct TerraTileNetworkJob : IJobParallelFor
	{
		public TerraGenSettings Settings;

		[ReadOnly] public NativeArray<TerraCell> Full;

		[NativeDisableParallelForRestriction] public NativeArray<TerraRiverSeg> Segs;    // take × SEG_CAP
		[NativeDisableParallelForRestriction] public NativeArray<int2>          Centers; // take × CENTER_CAP
		[NativeDisableParallelForRestriction] public NativeArray<int2>          Ranges;  // (start, count) per centre
		[NativeDisableParallelForRestriction] public NativeArray<int>           CenterCounts;

		public void Execute(int tile)
		{
			CenterCounts[tile] = 0;
			if (!TerraTileGen.UseNetworks(in Settings)) return;

			var levels   = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);
			var cellBase = tile * TerraTileGen.CELLS;
			var cBase    = tile * TerraTileGen.CENTER_CAP;
			var nCenters = 0;

			for (var i = 0; i < TerraTileGen.CELLS && nCenters < TerraTileGen.CENTER_CAP; i++)
			{
				TerraCell cell = Full[cellBase + i];
				if (cell.ContinentEdge < Settings.Inland) continue;

				var found = false;
				for (var c = 0; c < nCenters; c++)
					if (Centers[cBase + c].Equals(cell.ContinentCenter))
					{
						found = true;
						break;
					}

				if (!found) Centers[cBase + nCenters++] = cell.ContinentCenter;
			}

			var segs    = new NativeList<TerraRiverSeg>(256, Allocator.Temp);
			var segBase = tile * TerraTileGen.SEG_CAP;

			for (var c = 0; c < nCenters; c++)
			{
				var start = segs.Length;
				TerraRiverNet.BuildForCenter(Centers[cBase + c], in Settings, in levels, ref segs);
				var count = segs.Length - start;

				// clamp into the tile's fixed slice (BuildForCenter's own cap keeps the
				// total under SEG_CAP; this guards the theoretical overflow)
				if (start + count > TerraTileGen.SEG_CAP) count = TerraTileGen.SEG_CAP - start;
				if (count < 0) count = 0;
				Ranges[cBase + c] = new int2(segBase + start, count);
			}

			var copy = math.min(segs.Length, TerraTileGen.SEG_CAP);
			for (var i = 0; i < copy; i++) Segs[segBase + i] = segs[i];

			segs.Dispose();
			CenterCounts[tile] = nCenters;
		}
	}

	/// <summary>
	///     Stage 3 — one index per tile ROW: carve each column against its own continent's
	///     network, apply the climate noise stack (network path only — the simple path is
	///     already finished), then convert to the filter-stage cell.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct TerraTileCarveJob : IJobParallelFor
	{
		public TerraGenSettings Settings;

		[ReadOnly] public NativeArray<int2>          TileCoords;
		[ReadOnly] public NativeArray<TerraRiverSeg> Segs;
		[ReadOnly] public NativeArray<int2>          Centers;
		[ReadOnly] public NativeArray<int2>          Ranges;
		[ReadOnly] public NativeArray<int>           CenterCounts;

		[NativeDisableParallelForRestriction]                 public NativeArray<TerraCell>    Full;
		[NativeDisableParallelForRestriction] [WriteOnly]     public NativeArray<TerraGenCell> GenCells;

		public void Execute(int r)
		{
			var  tile   = r / TerraTileGen.SIZE;
			var  z      = r % TerraTileGen.SIZE;
			int2 origin = TerraTileConst.GenOrigin(TileCoords[tile]);
			var  levels = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);
			var  baseI  = tile * TerraTileGen.CELLS + z * TerraTileGen.SIZE;
			var  useNet = TerraTileGen.UseNetworks(in Settings);
			var  cBase  = tile * TerraTileGen.CENTER_CAP;

			for (var x = 0; x < TerraTileGen.SIZE; x++)
			{
				TerraCell cell = Full[baseI + x];

				if (useNet)
				{
					var wx = (float)(origin.x + x);
					var wz = (float)(origin.y + z);

					if (cell.ContinentEdge >= Settings.Inland)
						for (var c = 0; c < CenterCounts[tile]; c++)
							if (Centers[cBase + c].Equals(cell.ContinentCenter))
							{
								int2 range = Ranges[cBase + c];
								TerraRiverNet.CarveColumn(ref cell, wx, wz, in Segs, range.x, range.y,
								                          in Settings, in levels);
								break;
							}

					TerraHeightmap.ApplyClimate(ref cell, wx, wz, in Settings, in levels);
				}

				GenCells[baseI + x] = TerraTileGen.ToGenCell(in cell);
			}
		}
	}

	/// <summary>
	///     Stage 4 — one index per TILE: droplet erosion + smoothing (inherently
	///     sequential over the whole tile), river water settling, shoreline beach, and
	///     quantisation into the cache-owned TerraColumn array (via raw pointer, since
	///     IJobParallelFor can't hold a distinct NativeArray per index).
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public unsafe struct TerraTileFilterJob : IJobParallelFor
	{
		public TerraGenSettings Settings;

		[ReadOnly] public NativeArray<int2> TileCoords;

		[NativeDisableParallelForRestriction] public NativeArray<TerraGenCell> GenCells;

		[NativeDisableUnsafePtrRestriction] public NativeArray<IntPtr> ColumnPtrs;

		public void Execute(int tile)
		{
			var  size    = TerraTileGen.SIZE;
			int2 origin  = TerraTileConst.GenOrigin(TileCoords[tile]);
			var  levels  = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);
			var  columns = (TerraColumn*)ColumnPtrs[tile].ToPointer();

			// filters mutate a size²-origin-0 array — work on a Temp copy of this
			// tile's slice (≈0.9 MB) so their indexing stays untouched
			var cells = new NativeArray<TerraGenCell>(TerraTileGen.CELLS, Allocator.Temp,
			                                          NativeArrayOptions.UninitializedMemory);
			UnsafeUtility.MemCpy(cells.GetUnsafePtr(),
			                     (TerraGenCell*)GenCells.GetUnsafeReadOnlyPtr() + tile * TerraTileGen.CELLS,
			                     (long)TerraTileGen.CELLS * UnsafeUtility.SizeOf<TerraGenCell>());

			// ── filters (RTF WorldFilters.applyOptionalFilters order) ──────────
			TerraErosion.ApplyErosion(ref cells, size, origin.x, origin.y, in Settings, in levels);
			TerraErosion.ApplySmoothing(ref cells, size, in Settings, in levels);

			// ── block-space conversion + river water settling ──────────────────
			var surfBlocks = new NativeArray<float>(TerraTileGen.CELLS, Allocator.Temp,
			                                        NativeArrayOptions.UninitializedMemory);
			var waterBlocks = new NativeArray<float>(TerraTileGen.CELLS, Allocator.Temp,
			                                         NativeArrayOptions.UninitializedMemory);
			for (var i = 0; i < cells.Length; i++)
			{
				TerraGenCell cell = cells[i];
				surfBlocks[i] = levels.ToBlocksF(cell.Height);
				waterBlocks[i] = cell.Terrain == TerraTerrain.River
					? levels.ToBlocksF(cell.RiverWaterLevel)
					: 0f;
			}

			TerraRivers.SettleWater(in cells, ref surfBlocks, ref waterBlocks, size);

			// sand the ocean shoreline so the coast reads water → sand → grass
			TerraRivers.ShorelineBeach(ref cells, in surfBlocks, size);

			// ── quantise to columns ────────────────────────────────────────────
			for (var i = 0; i < cells.Length; i++)
			{
				TerraGenCell cell = cells[i];
				var waterY = 0; // sea level
				if (cell.Terrain == TerraTerrain.River)
					waterY = math.max(0, TerraNoise.Round(waterBlocks[i]));

				columns[i] = new TerraColumn
				             {
					             SurfaceY  = TerraNoise.Round(surfBlocks[i]),
					             WaterY    = waterY,
					             RiverMask = cell.RiverMask,
					             Biome     = cell.Biome,
					             Terrain   = cell.Terrain
				             };
			}

			surfBlocks.Dispose();
			waterBlocks.Dispose();
			cells.Dispose();
		}
	}
}
