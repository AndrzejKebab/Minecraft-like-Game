using Unity.Burst;

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
	///     Column → voxel classification. Column data itself is produced by
	///     TerraTileGenJob and cached per tile in TerraTileCacheSingleton.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class TerraGenerator
	{
		// block prototype IDs (see Resources/Blocks assets)
		public const ushort AIR   = 0;
		public const ushort STONE = 1;
		public const ushort DIRT  = 2;
		public const ushort GRASS = 3;
		public const ushort WATER = 4;
		public const ushort SAND  = 5;

		/// <summary>
		///     Biome/terrain-aware voxel classification. Rocky peaks above the stone
		///     line, sand in deserts / badlands / beaches / river beds, grass+dirt
		///     elsewhere, sea + river water above the surface.
		/// </summary>
		public static ushort ClassifyVoxel(int worldY, in TerraColumn column, int stoneLineY)
		{
			var surface = column.SurfaceY;
			var water   = column.WaterY;

			if (worldY > surface)
				return worldY <= water ? WATER : AIR;

			var depth      = surface - worldY;
			var submerged  = surface < water;

			// river beds are handled by the `submerged` path below (sand under
			// water); River terrain itself is never dry, so it isn't a `sandy`
			// surface — its banks are ordinary grass/dirt
			var sandy = column.Terrain == TerraTerrain.Beach ||
			            column.Terrain == TerraTerrain.Badlands ||
			            column.Biome == TerraBiome.Desert;

			var rocky = surface >= stoneLineY && column.Terrain.IsMountain();

			return depth switch
			       {
				       0 when rocky        => STONE,
				       0 when submerged    => column.Terrain == TerraTerrain.DeepOcean ? STONE : SAND,
				       0                   => sandy ? SAND : GRASS,
				       <= 4 when rocky     => STONE,
				       <= 4 when submerged => column.Terrain == TerraTerrain.DeepOcean ? STONE : SAND,
				       <= 4                => sandy ? SAND : DIRT,
				       _                   => STONE
			       };
		}
	}
}
