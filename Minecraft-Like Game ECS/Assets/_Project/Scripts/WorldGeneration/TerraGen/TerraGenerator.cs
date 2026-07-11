using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary> Per-column result consumed by chunk population. 16 bytes. </summary>
	public struct TerraColumn
	{
		public int          SurfaceY;  // world Y of the top solid block
		public int          WaterY;    // world Y of the water surface (0 = sea level)
		public float        RiverMask; // 1 away from rivers → 0 at channel centre
		public TerraBiome   Biome;
		public TerraTerrain Terrain;
	}

	/// <summary>
	///     Bridge between the RTF-style cell pipeline and the chunk population job:
	///     fills the 3×3-chunk halo of columns (same pattern the old NoiseGenerator
	///     used) and classifies voxels against a column.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class TerraGenerator
	{
		public static void GenerateHaloColumns(out NativeArray<TerraColumn> columns,
		                                       ref int3 chunkWorldPos, int chunkSize,
		                                       in TerraGenSettings settings)
		{
			var haloSize = chunkSize * 3;
			var originX  = chunkWorldPos.x - chunkSize;
			var originZ  = chunkWorldPos.z - chunkSize;

			columns = new NativeArray<TerraColumn>(haloSize * haloSize, Allocator.Temp,
			                                       NativeArrayOptions.UninitializedMemory);

			var levels = TerraLevels.Make(settings.WorldHeight, settings.SeaLevel);

			for (var z = 0; z < haloSize; z++)
			for (var x = 0; x < haloSize; x++)
			{
				TerraHeightmap.Sample(out TerraCell cell, originX + x, originZ + z,
				                      in settings, in levels);

				var surfaceY = levels.ToBlockY(cell.Height);
				var waterY   = 0; // sea level
				if (cell.Terrain == TerraTerrain.River && cell.RiverWaterLevel > 0f)
					waterY = math.max(0, levels.ToBlockY(cell.RiverWaterLevel));

				columns[x + z * haloSize] = new TerraColumn
				                            {
					                            SurfaceY  = surfaceY,
					                            WaterY    = waterY,
					                            RiverMask = cell.RiverMask,
					                            Biome     = cell.Biome,
					                            Terrain   = cell.Terrain
				                            };
			}
		}

		// block prototype IDs (see Resources/Blocks assets)
		private const ushort AIR   = 0;
		private const ushort STONE = 1;
		private const ushort DIRT  = 2;
		private const ushort GRASS = 3;
		private const ushort WATER = 4;
		private const ushort SAND  = 5;

		/// <summary>
		///     Biome/terrain-aware voxel classification (replaces the old flat
		///     ClassifyVoxel). Rocky peaks above the stone line, sand in deserts /
		///     badlands / beaches / river beds, grass+dirt elsewhere.
		/// </summary>
		public static ushort ClassifyVoxel(int worldY, in TerraColumn column, int stoneLineY)
		{
			var surface = column.SurfaceY;
			var water   = column.WaterY;

			if (worldY > surface)
				return worldY <= water ? WATER : AIR;

			var depth      = surface - worldY;
			var submerged  = surface < water;

			var sandy = column.Terrain == TerraTerrain.Beach ||
			            column.Terrain == TerraTerrain.River ||
			            column.Terrain == TerraTerrain.Badlands ||
			            column.Biome == TerraBiome.Desert;

			var rocky = surface >= stoneLineY && column.Terrain.IsMountain();

			if (depth == 0)
			{
				if (rocky) return STONE;
				if (submerged)
					return column.Terrain == TerraTerrain.DeepOcean ? STONE : SAND;
				return sandy ? SAND : GRASS;
			}

			if (depth <= 4)
			{
				if (rocky) return STONE;
				if (submerged)
					return column.Terrain == TerraTerrain.DeepOcean ? STONE : SAND;
				return sandy ? SAND : DIRT;
			}

			return STONE;
		}
	}
}
