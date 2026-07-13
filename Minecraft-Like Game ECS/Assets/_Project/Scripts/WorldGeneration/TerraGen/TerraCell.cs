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
		public int2         ContinentCenter; // corrected voronoi centre (river networks key)
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
	///     Port of RTF Levels, decoupled from output scale.
	///     The pipeline runs in a FIXED virtual space (1024 units, sea at 256 —
	///     RTF's proportions, which the populator constants are tuned for). Output
	///     block heights come from a hypsometric curve, like real-world elevation
	///     distribution: most land is low and gentle, mountainsides steepen
	///     exponentially. Sea level is ALWAYS world Y 0.
	///     OceanDepth = blocks from sea to the deepest floor.
	///     MountainHeight = blocks at normalised elevation 1.0 (a typical big
	///     mountain); rare peaks reach ~2×.
	/// </summary>
	public struct TerraLevels
	{
		public const int VIRTUAL_HEIGHT = 1024;
		public const int VIRTUAL_SEA    = 256;

		public int   OceanDepth;
		public int   MountainHeight;
		public float Unit;   // 1 / VIRTUAL_HEIGHT
		public float Water;  // normalised water level
		public float Ground; // normalised first-land level

		public static TerraLevels Make(int oceanDepth, int mountainHeight)
		{
			return new TerraLevels
			       {
				       OceanDepth     = math.max(1, oceanDepth),
				       MountainHeight = math.max(1, mountainHeight),
				       Unit           = 1f / VIRTUAL_HEIGHT,
				       Water          = (VIRTUAL_SEA - 1) / (float)VIRTUAL_HEIGHT,
				       Ground         = VIRTUAL_SEA / (float)VIRTUAL_HEIGHT
			       };
		}

		/// <summary> Virtual units → normalised delta (populator-space, pre-curve). </summary>
		public float Scale(int virtualBlocks)
		{
			return virtualBlocks / (float)VIRTUAL_HEIGHT;
		}

		public float WaterPlus(int virtualBlocks)
		{
			return (VIRTUAL_SEA - 1 + virtualBlocks) / (float)VIRTUAL_HEIGHT;
		}

		// ── hypsometric curve ────────────────────────────────────────────────
		// e = elevation fraction above water (h - Water)/(1 - Water), f in units
		// of MountainHeight. Piecewise linear, monotonic, slope increases with
		// altitude: flat plains, rolling hills, steep peaks.
		//   e:    0.00  0.10  0.30  0.60  1.00  1.45+
		//   f:    0.00  0.02  0.10  0.30  1.00  2.00  (then slope 2.2)

		private static float CurveF(float e)
		{
			if (e <= 0f) return 0f;
			if (e < 0.10f) return e * (0.02f / 0.10f);
			if (e < 0.30f) return 0.02f + (e - 0.10f) * ((0.10f - 0.02f) / 0.20f);
			if (e < 0.60f) return 0.10f + (e - 0.30f) * ((0.30f - 0.10f) / 0.30f);
			if (e < 1.00f) return 0.30f + (e - 0.60f) * ((1.00f - 0.30f) / 0.40f);
			if (e < 1.45f) return 1.00f + (e - 1.00f) * ((2.00f - 1.00f) / 0.45f);
			return 2.00f + (e - 1.45f) * 2.2f;
		}

		private static float CurveSlope(float e)
		{
			if (e < 0.10f) return 0.02f / 0.10f;
			if (e < 0.30f) return (0.10f - 0.02f) / 0.20f;
			if (e < 0.60f) return (0.30f - 0.10f) / 0.30f;
			if (e < 1.00f) return (1.00f - 0.30f) / 0.40f;
			if (e < 1.45f) return (2.00f - 1.00f) / 0.45f;
			return 2.2f;
		}

		/// <summary> Inverse of CurveF (f in MountainHeight units → e). </summary>
		private static float CurveInv(float f)
		{
			if (f <= 0f) return 0f;
			if (f < 0.02f) return f * (0.10f / 0.02f);
			if (f < 0.10f) return 0.10f + (f - 0.02f) * (0.20f / (0.10f - 0.02f));
			if (f < 0.30f) return 0.30f + (f - 0.10f) * (0.30f / (0.30f - 0.10f));
			if (f < 1.00f) return 0.60f + (f - 0.30f) * (0.40f / (1.00f - 0.30f));
			if (f < 2.00f) return 1.00f + (f - 1.00f) * (0.45f / (2.00f - 1.00f));
			return 1.45f + (f - 2.00f) / 2.2f;
		}

		/// <summary> Inverse of OceanF (f in OceanDepth units → d). </summary>
		private static float OceanInv(float f)
		{
			if (f <= 0f) return 0f;
			if (f < 0.03f) return f * (0.15f / 0.03f);
			if (f < 0.35f) return 0.15f + (f - 0.03f) * (0.35f / (0.35f - 0.03f));
			return 0.50f + (f - 0.35f) * (0.50f / (1.00f - 0.35f));
		}

		/// <summary> Normalised height → world blocks (float, no rounding). </summary>
		public float ToBlocksF(float normalised)
		{
			if (normalised <= Water)
				return -OceanF((Water - normalised) / Water) * OceanDepth;
			return CurveF((normalised - Water) / (1f - Water)) * MountainHeight;
		}

		/// <summary> World blocks → normalised height (inverse of ToBlocksF). </summary>
		public float FromBlocksF(float blocks)
		{
			if (blocks <= 0f)
				return Water - OceanInv(-blocks / OceanDepth) * Water;
			return Water + CurveInv(blocks / MountainHeight) * (1f - Water);
		}

		// ── ocean curve ──────────────────────────────────────────────────────
		// d = depth fraction below water, f in units of OceanDepth. Gentle shelf
		// near the shore (so slightly-sub-sea inland dips become shallow marshes
		// and lakes, not 20-block-deep seas), steepening toward the abyss.
		//   d:    0.00  0.15  0.50  1.00
		//   f:    0.00  0.03  0.35  1.00

		private static float OceanF(float d)
		{
			if (d <= 0f) return 0f;
			if (d < 0.15f) return d * (0.03f / 0.15f);
			if (d < 0.50f) return 0.03f + (d - 0.15f) * ((0.35f - 0.03f) / 0.35f);
			if (d < 1.00f) return 0.35f + (d - 0.50f) * ((1.00f - 0.35f) / 0.50f);
			return 1f;
		}

		private static float OceanSlope(float d)
		{
			if (d < 0.15f) return 0.03f / 0.15f;
			if (d < 0.50f) return (0.35f - 0.03f) / 0.35f;
			return (1.00f - 0.35f) / 0.50f;
		}

		/// <summary> Normalised height → world block Y (sea level = world Y 0). </summary>
		public int ToBlockY(float normalised)
		{
			if (normalised <= Water)
			{
				var d = (Water - normalised) / Water;
				return -TerraNoise.Round(OceanF(d) * OceanDepth);
			}

			var e = (normalised - Water) / (1f - Water);
			return TerraNoise.Round(CurveF(e) * MountainHeight);
		}

		/// <summary>
		///     Normalised-height delta that produces `blocks` of world height at
		///     height h — lets carving (rivers) work in real block units through
		///     the curve.
		/// </summary>
		public float NormForBlocks(float h, float blocks)
		{
			float blocksPerNorm;
			if (h <= Water)
				blocksPerNorm = OceanSlope((Water - h) / Water) * OceanDepth / Water;
			else
				blocksPerNorm = CurveSlope((h - Water) / (1f - Water)) * MountainHeight / (1f - Water);
			return blocks / blocksPerNorm;
		}

		/// <summary> Normalised delta for `blocks` of height just above the sea. </summary>
		public float BlocksAboveSea(float blocks)
		{
			return NormForBlocks(Ground, blocks);
		}
	}
}
