using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary> Port of RTF TerrainType/TerrainCategory (flattened to one enum). </summary>
	public enum TerraTerrain : byte
	{
		None         = 0,
		DeepOcean    = 1,
		ShallowOcean = 2,
		Coast        = 3,
		Beach        = 4,
		River        = 5,
		Flats        = 6,
		Badlands     = 7,
		Plateau      = 8,
		Hills        = 9,
		Mountains1   = 10,
		Mountains2   = 11,
		Mountains3   = 12,
		MountainChain = 13
	}

	/// <summary> Port of RTF BiomeType (Whittaker-style temperature × moisture classes). </summary>
	public enum TerraBiome : byte
	{
		TropicalRainforest  = 0,
		Savanna             = 1,
		Desert              = 2,
		TemperateRainforest = 3,
		TemperateForest     = 4,
		Grassland           = 5,
		ColdSteppe          = 6,
		Steppe              = 7,
		Taiga               = 8,
		Tundra              = 9,
		Alpine              = 10
	}

	public static class TerraTerrainExt
	{
		public static bool IsSubmerged(this TerraTerrain t)
		{
			return t == TerraTerrain.DeepOcean || t == TerraTerrain.ShallowOcean || t == TerraTerrain.River;
		}

		public static bool IsMountain(this TerraTerrain t)
		{
			return t >= TerraTerrain.Mountains1 && t <= TerraTerrain.MountainChain;
		}

		public static bool IsOverground(this TerraTerrain t)
		{
			return t >= TerraTerrain.Coast;
		}

		public static bool OverridesCoast(this TerraTerrain t)
		{
			// RTF: mountains + badlands keep their identity at the coastline
			return t.IsMountain() || t == TerraTerrain.Badlands;
		}
	}

	/// <summary>
	///     Blittable port of RTF's Cell — the per-column bag of intermediate values the
	///     generation stages read and write. Lives on the stack inside jobs.
	/// </summary>
	public struct TerraCell
	{
		public float        Height;      // normalised [0,1] against WorldHeight
		public float        ContinentId;
		public float        ContinentEdge;
		public float        ContinentDistance;
		public float        TerrainRegionId;
		public float        TerrainRegionEdge;
		public float2       TerrainRegionCenter; // world-space voronoi centre (climate override)
		public float        BiomeRegionId;
		public float        BiomeRegionEdge;
		public float        MacroBiomeId;
		public float        RiverMask;   // 1 away from rivers → 0 at the channel centre
		public float        RiverWaterLevel; // normalised; 0 when not in a river valley
		public float        RegionTemperature;
		public float        RegionMoisture;
		public float        Temperature;
		public float        Moisture;
		public float        Erosion;     // biome-parameter erosion (not the filter)
		public float        Weirdness;
		public float        BeachNoise;
		public float        Gradient;
		public TerraTerrain Terrain;
		public TerraBiome   Biome;

		public static TerraCell Default()
		{
			return new TerraCell
			       {
				       RegionTemperature = 0.5f,
				       RegionMoisture    = 0.5f,
				       BiomeRegionEdge   = 1f,
				       RiverMask         = 1f,
				       Terrain           = TerraTerrain.None,
				       Biome             = TerraBiome.Grassland
			       };
		}
	}

	/// <summary>
	///     Port of RTF Levels — converts between normalised heights [0,1] and block Ys.
	///     WorldY 0 in this game is sea level, so ToBlockY subtracts SeaLevel.
	/// </summary>
	public struct TerraLevels
	{
		public int   WorldHeight;
		public int   SeaLevel;
		public float Unit;   // 1 / WorldHeight
		public float Water;  // normalised water level
		public float Ground; // normalised first-land level

		public static TerraLevels Make(int worldHeight, int seaLevel)
		{
			var height = math.max(1, worldHeight);
			return new TerraLevels
			       {
				       WorldHeight = height,
				       SeaLevel    = seaLevel,
				       Unit        = 1f / height,
				       Water       = (seaLevel - 1) / (float)height,
				       Ground      = seaLevel / (float)height
			       };
		}

		public float Scale(int blocks)
		{
			return blocks / (float)WorldHeight;
		}

		public float WaterPlus(int blocks)
		{
			return (SeaLevel - 1 + blocks) / (float)WorldHeight;
		}

		/// <summary> Normalised height → world block Y (sea level = worldY 0). </summary>
		public int ToBlockY(float normalised)
		{
			return TerraNoise.Round(normalised * WorldHeight) - SeaLevel;
		}
	}
}
