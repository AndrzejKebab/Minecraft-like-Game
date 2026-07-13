using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Generates one tile: samples the RTF cell pipeline over the bordered grid,
	///     carves the branching river networks of the continents overlapping the tile,
	///     runs the droplet erosion + smoothing filters (RTF WorldFilters order), then
	///     quantises to TerraColumns. One IJob per tile — tiles parallelise across
	///     worker threads, and results are cached so the N vertical chunks of every
	///     column (and all 16 chunk columns of the tile) reuse one generation.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct TerraTileGenJob : IJob
	{
		public int2             TileCoord;
		public TerraGenSettings Settings;

		/// <summary> Output: bordered GEN_BLOCKS² grid owned by the tile cache. </summary>
		public NativeArray<TerraColumn> Columns;

		public void Execute()
		{
			var  size   = TerraTileConst.GEN_BLOCKS;
			int2 origin = TerraTileConst.GenOrigin(TileCoord);
			var  levels = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);

			var cells = new NativeArray<TerraGenCell>(size * size, Allocator.Temp,
			                                          NativeArrayOptions.UninitializedMemory);

			if (Settings.RiversEnabled && Settings.UseRiverNetworks)
				GenerateWithNetworks(ref cells, size, origin, in levels);
			else
				GenerateSimple(ref cells, size, origin, in levels);

			// ── filters (RTF WorldFilters.applyOptionalFilters order) ──────────
			TerraErosion.ApplyErosion(ref cells, size, origin.x, origin.y, in Settings, in levels);
			TerraErosion.ApplySmoothing(ref cells, size, in Settings, in levels);

			// ── block-space conversion + river water settling ──────────────────
			var surfBlocks  = new NativeArray<float>(size * size, Allocator.Temp,
			                                         NativeArrayOptions.UninitializedMemory);
			var waterBlocks = new NativeArray<float>(size * size, Allocator.Temp,
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

			// ── quantise to columns ────────────────────────────────────────────
			for (var i = 0; i < cells.Length; i++)
			{
				TerraGenCell cell = cells[i];
				var waterY = 0; // sea level
				if (cell.Terrain == TerraTerrain.River)
					waterY = math.max(0, TerraNoise.Round(waterBlocks[i]));

				Columns[i] = new TerraColumn
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

		/// <summary> Fallback per-column voronoi rivers (TerraHeightmap.Sample). </summary>
		private void GenerateSimple(ref NativeArray<TerraGenCell> cells, int size, int2 origin,
		                            in TerraLevels levels)
		{
			for (var z = 0; z < size; z++)
			for (var x = 0; x < size; x++)
			{
				TerraHeightmap.Sample(out TerraCell cell, origin.x + x, origin.y + z,
				                      in Settings, in levels);
				cells[x + z * size] = ToGenCell(in cell);
			}
		}

		/// <summary>
		///     RTF-style path: terrain everywhere, build each overlapping continent's
		///     river network once, carve the columns against their own continent's
		///     network, then climate.
		/// </summary>
		private void GenerateWithNetworks(ref NativeArray<TerraGenCell> cells, int size, int2 origin,
		                                  in TerraLevels levels)
		{
			var full = new NativeArray<TerraCell>(size * size, Allocator.Temp,
			                                      NativeArrayOptions.UninitializedMemory);

			// unique continent centres overlapping the tile → parallel network ranges
			var centers = new NativeList<int2>(8, Allocator.Temp);
			var ranges  = new NativeList<int2>(8, Allocator.Temp); // (start, count) into segs
			var segs    = new NativeList<TerraRiverSeg>(256, Allocator.Temp);

			// ── stage 1: terrain, collect centres ──────────────────────────────
			for (var z = 0; z < size; z++)
			for (var x = 0; x < size; x++)
			{
				var i = x + z * size;
				TerraHeightmap.SampleTerrain(out TerraCell cell, origin.x + x, origin.y + z,
				                             in Settings, in levels);
				full[i] = cell;

				if (cell.ContinentEdge < Settings.Inland) continue;
				var found = false;
				for (var c = 0; c < centers.Length; c++)
					if (centers[c].Equals(cell.ContinentCenter))
					{
						found = true;
						break;
					}

				if (!found) centers.Add(cell.ContinentCenter);
			}

			// ── stage 2: build a network per continent ─────────────────────────
			for (var c = 0; c < centers.Length; c++)
			{
				var startSeg = segs.Length;
				TerraRiverNet.BuildForCenter(centers[c], in Settings, in levels, ref segs);
				ranges.Add(new int2(startSeg, segs.Length - startSeg));
			}

			NativeArray<TerraRiverSeg> segArray = segs.AsArray();

			// ── stage 3: carve own-continent network + climate ─────────────────
			for (var z = 0; z < size; z++)
			for (var x = 0; x < size; x++)
			{
				var       i    = x + z * size;
				TerraCell cell = full[i];
				var       wx   = (float)(origin.x + x);
				var       wz   = (float)(origin.y + z);

				if (cell.ContinentEdge >= Settings.Inland)
				{
					// find this column's continent network range
					for (var c = 0; c < centers.Length; c++)
						if (centers[c].Equals(cell.ContinentCenter))
						{
							int2 r = ranges[c];
							TerraRiverNet.CarveColumn(ref cell, wx, wz, in segArray, r.x, r.y,
							                          in Settings, in levels);
							break;
						}
				}

				TerraHeightmap.ApplyClimate(ref cell, wx, wz, in Settings, in levels);
				cells[i] = ToGenCell(in cell);
			}

			segs.Dispose();
			ranges.Dispose();
			centers.Dispose();
			full.Dispose();
		}

		private static TerraGenCell ToGenCell(in TerraCell cell)
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
	}
}
