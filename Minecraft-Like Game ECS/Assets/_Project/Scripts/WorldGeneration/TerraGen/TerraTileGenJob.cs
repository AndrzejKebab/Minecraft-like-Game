using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Generates one tile: samples the RTF cell pipeline over the bordered grid,
	///     runs the droplet erosion + smoothing filters (RTF WorldFilters order),
	///     then quantises to TerraColumns. One IJob per tile — tiles parallelise
	///     across worker threads, and results are cached so the N vertical chunks of
	///     every column (and all 16 chunk columns of the tile) reuse one generation.
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
			var size   = TerraTileConst.GEN_BLOCKS;
			int2 origin = TerraTileConst.GenOrigin(TileCoord);
			var levels = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);

			var cells = new NativeArray<TerraGenCell>(size * size, Allocator.Temp,
			                                          NativeArrayOptions.UninitializedMemory);

			// ── 1. sample the cell pipeline ────────────────────────────────────
			for (var z = 0; z < size; z++)
			for (var x = 0; x < size; x++)
			{
				TerraHeightmap.Sample(out TerraCell cell, origin.x + x, origin.y + z,
				                      in Settings, in levels);
				cells[x + z * size] = new TerraGenCell
				                      {
					                      Height          = cell.Height,
					                      RiverMask       = cell.RiverMask,
					                      RiverWaterLevel = cell.RiverWaterLevel,
					                      RegionEdge      = cell.TerrainRegionEdge,
					                      Terrain         = cell.Terrain,
					                      Biome           = cell.Biome
				                      };
			}

			// ── 2. filters (RTF WorldFilters.applyOptionalFilters order) ───────
			TerraErosion.ApplyErosion(ref cells, size, origin.x, origin.y, in Settings, in levels);
			TerraErosion.ApplySmoothing(ref cells, size, in Settings, in levels);

			// ── 3. block-space conversion + river water settling ───────────────
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

			// ── 4. quantise to columns ─────────────────────────────────────────
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
	}
}
