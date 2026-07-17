using Unity.Burst;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Port of RTF ClimateModule + Climate + BiomeType.
	///     A biome-scale voronoi assigns each cell to a climate region; temperature
	///     (latitude sine bands, LegacyTemperature) and moisture (large simplex,
	///     LegacyMoisture) are sampled at the region centre so a whole region shares
	///     one biome. The Whittaker table (BiomeType.getCurve) maps temp × moisture
	///     to a biome; RTF loads it from an image, here it's the equivalent band grid.
	/// </summary>
	[BurstCompile]
	public static class TerraClimate
	{
		private const int SEED_CLIMATE = 500_000;
		private const int SEED_WARP_X  = 1;
		private const int SEED_WARP_Z  = 2;
		private const int SEED_MOIST   = 10;
		private const int SEED_TEMP    = 20;
		private const int SEED_MACRO   = 30;
		private const int SEED_OFFSET  = 40;

		private const float EDGE_BLEND      = 0.4f;
		private const int   OFFSET_DISTANCE = 100; // biomeEdgeShape.strength

		/// <summary> Climate.apply — includes the ragged biome-edge offset pass. </summary>
		public static void Apply(ref TerraCell cell, float x, float z, in TerraGenSettings s,
		                         in TerraLevels levels)
		{
			ApplyModule(ref cell, x, z, true, in s, in levels);

			if (cell.Height <= levels.Water)
			{
				if (cell.Terrain == TerraTerrain.Coast)
					cell.Terrain = TerraTerrain.ShallowOcean;
			}
			else if (cell.BiomeRegionEdge < EDGE_BLEND || cell.Terrain == TerraTerrain.MountainChain)
			{
				var seed     = s.Seed + SEED_CLIMATE;
				var modifier = 1f - TerraNoise.Map(cell.BiomeRegionEdge, 0f, EDGE_BLEND, EDGE_BLEND);
				var distance = OFFSET_DISTANCE * modifier;
				var dx = TerraNoise.PerlinSigned(x, z, seed + SEED_OFFSET, 1f / 150f, 2) * distance;
				var dz = TerraNoise.PerlinSigned(x, z, seed + SEED_OFFSET + 1, 1f / 150f, 2) * distance;
				ApplyModule(ref cell, x + dx, z + dz, false, in s, in levels);
			}
		}

		/// <summary> ClimateModule.apply. </summary>
		public static void ApplyModule(ref TerraCell cell, float x, float z, bool mask,
		                               in TerraGenSettings s, in TerraLevels levels)
		{
			var seed      = s.Seed + SEED_CLIMATE;
			var biomeFreq = 1f / math.max(1, s.BiomeSize);

			// biome-shape warp
			var ox = TerraNoise.PerlinSigned(x, z, seed + SEED_WARP_X, 1f / s.BiomeWarpScale, 2)
			         * s.BiomeWarpStrength;
			var oz = TerraNoise.PerlinSigned(x, z, seed + SEED_WARP_Z, 1f / s.BiomeWarpScale, 2)
			         * s.BiomeWarpStrength;
			var px = (x + ox) * biomeFreq;
			var pz = (z + oz) * biomeFreq;

			// biome voronoi
			var xr = TerraNoise.Floor(px);
			var zr = TerraNoise.Floor(pz);
			var cellX = xr;
			var cellZ = zr;
			var centerX = px;
			var centerZ = pz;
			var edgeDistance  = float.MaxValue;
			var edgeDistance2 = float.MaxValue;
			for (var dz = -1; dz <= 1; dz++)
			for (var dx = -1; dx <= 1; dx++)
			{
				var    cx  = xr + dx;
				var    cz  = zr + dz;
				float2 vec = TerraNoise.CellVec(seed, cx, cz);
				var    cxf = cx + vec.x;
				var    czf = cz + vec.y;
				var    d   = TerraNoise.DistApply(TerraNoise.DistFunc.Euclidean, cxf - px, czf - pz);
				if (d < edgeDistance)
				{
					edgeDistance2 = edgeDistance;
					edgeDistance  = d;
					centerX       = cxf;
					centerZ       = czf;
					cellX         = cx;
					cellZ         = cz;
				}
				else if (d < edgeDistance2)
				{
					edgeDistance2 = d;
				}
			}

			cell.BiomeRegionId     = 0.5f + TerraNoise.ValCoord2D(seed, cellX, cellZ) * 0.5f;
			cell.RegionMoisture    = Moisture(centerX, centerZ, seed, in s);
			cell.RegionTemperature = Temperature(centerX, centerZ, seed, in s);
			cell.MacroBiomeId      = TerraNoise.CellValue01(seed + SEED_MACRO, cellX, cellZ);

			// RTF samples the continent edge at the biome centre so moisture is uniform
			// per region; we reuse the cell's own edge value (cheaper, slight gradient)
			var continentEdge = cell.ContinentEdge;
			if (mask)
			{
				edgeDistance  = math.sqrt(edgeDistance);
				edgeDistance2 = math.sqrt(edgeDistance2);
				cell.BiomeRegionEdge = 1f - TerraNoise.Map(edgeDistance / math.max(1e-6f, edgeDistance2) - 1f,
				                                           -1f, 0f, 1f);
				ModifyTerrain(ref cell, continentEdge, in s);
			}

			cell.RegionMoisture = ModifyMoisture(cell.RegionMoisture, continentEdge);

			cell.Biome             = GetBiome(cell.RegionTemperature, cell.RegionMoisture);
			cell.RegionTemperature = ModifyTemp(cell.Height, cell.RegionTemperature, in levels);
			cell.Temperature       = BiomeTemperature(cell.Biome);
			cell.Moisture          = BiomeMoisture(cell.Biome);

			// mountain override: sample climate at the terrain-region centre so a whole
			// mountain region shares one biome
			if (!cell.Terrain.IsMountain()) return;
			var mtnX     = cell.TerrainRegionCenter.x * biomeFreq;
			var mtnZ     = cell.TerrainRegionCenter.y * biomeFreq;
			var mtnTemp  = Temperature(mtnX, mtnZ, seed, in s);
			var mtnMoist = Moisture(mtnX, mtnZ, seed, in s);
			cell.Biome       = GetBiome(mtnTemp, mtnMoist);
			cell.Temperature = BiomeTemperature(cell.Biome);
			cell.Moisture    = BiomeMoisture(cell.Biome);
		}

		// ── Temperature / moisture fields (biome-cell space coordinates) ──────

		/// <summary> LegacyTemperature: latitude sine bands + warp, [0,1]. </summary>
		private static float Temperature(float x, float z, int seed, in TerraGenSettings s)
		{
			var tempScale = math.max(1, TerraNoise.Round(s.TemperatureScale));

			// warpPerlin(tempScale*4, 2, tempScale*4) then warpPerlin(tempScale, 1, tempScale)
			TerraNoise.WarpPerlin(ref x, ref z, seed + SEED_TEMP + 1, tempScale * 4, 2, tempScale * 4);
			TerraNoise.WarpPerlin(ref x, ref z, seed + SEED_TEMP + 2, tempScale, 1, tempScale);

			var sin = math.sin(z * (1f / tempScale));
			sin = math.clamp(sin, -1f, 1f);
			var value = math.pow(math.abs(sin), s.TemperatureFalloff);
			value = TerraNoise.CopySign(value, sin);
			return TerraNoise.Map(value, -1f, 1f, 2f);
		}

		/// <summary> Moisture field, [0,1]. </summary>
		private static float Moisture(float x, float z, int seed, in TerraGenSettings s)
		{
			var moistScale = math.max(1, TerraNoise.Round(s.MoistureScale * 2.5f));

			TerraNoise.WarpPerlin(ref x, ref z, seed + SEED_MOIST + 1,
			                      math.max(1, moistScale / 2), 1, moistScale / 4f);
			TerraNoise.WarpPerlin(ref x, ref z, seed + SEED_MOIST + 2,
			                      math.max(1, moistScale / 6), 2, moistScale / 12f);

			var value = TerraNoise.Perlin(x, z, seed + SEED_MOIST, 1f / moistScale, 1);
			value = TerraNoise.Map(value, 0.125f, 0.875f, 0.75f);
			return value;
		}

		private static float ModifyTemp(float height, float temp, in TerraLevels levels)
		{
			switch (height)
			{
				case > 0.75f:
					return math.max(0f, temp - 0.05f);
				case > 0.45f:
				{
					var delta = (height - 0.45f) / 0.3f;
					return math.max(0f, temp - delta * 0.05f);
				}
			}

			height = math.max(levels.Ground, height);
			var warmDelta = 1f - (height - levels.Ground) / (0.45f - levels.Ground);
			return math.min(1f, temp + warmDelta * 0.05f);
		}

		private static float ModifyMoisture(float moisture, float continentEdge)
		{
			const float limit = 0.75f;
			const float range = 1f - limit;
			if (continentEdge < limit)
			{
				var alpha      = (limit - continentEdge) / range;
				var multiplier = 1f + alpha * range;
				return math.clamp(moisture * multiplier, 0f, 1f);
			}
			else
			{
				var alpha      = (continentEdge - limit) / range;
				var multiplier = 1f - alpha * range;
				return moisture * multiplier;
			}
		}

		private static void ModifyTerrain(ref TerraCell cell, float continentEdge, in TerraGenSettings s)
		{
			if (cell.Terrain.IsOverground() && !cell.Terrain.OverridesCoast() && continentEdge <= s.Coast)
				cell.Terrain = TerraTerrain.Coast;
		}

		// ── Whittaker biome table (BiomeType port) ────────────────────────────

		/// <summary>
		///     BiomeType.getCurve: moisture is capped by temperature (dry when cold),
		///     then both are classified into RTF's Temperature/Humidity level bands.
		/// </summary>
		public static TerraBiome GetBiome(float temperature, float moisture)
		{
			// getCurve skew
			var x   = TerraNoise.Round(255f * math.saturate(temperature));
			var max = x + (255 - x) / 2;
			var y   = math.min(x, TerraNoise.Round(max * math.saturate(moisture)));

			// classify into level bands; params live in [-1,1]
			var tempParam  = x / 255f * 2f - 1f;
			var moistParam = y / 255f * 2f - 1f;

			var t = TempLevel(tempParam);
			var h = HumidityLevel(moistParam);

			switch (t)
			{
				case 0: return h >= 4 ? TerraBiome.ColdSteppe : TerraBiome.Tundra;
				case 1:
					return h switch
					       {
						       <= 0 => TerraBiome.Grassland,
						       <= 2 => TerraBiome.Steppe,
						       _    => TerraBiome.Taiga
					       };
				case 2:
					return h switch
					       {
						       <= 0 => TerraBiome.Grassland,
						       <= 3 => TerraBiome.TemperateForest,
						       _    => TerraBiome.TemperateRainforest
					       };
				case 3:
					return h <= 2 ? TerraBiome.Savanna : TerraBiome.TropicalRainforest;
				default:
					return TerraBiome.Desert;
			}
		}

		private static int TempLevel(float v)
		{
			return v switch
			       {
				       // Temperature LEVEL_0..4 band edges
				       < -0.45f => 0,
				       < -0.15f => 1,
				       < 0.2f   => 2,
				       < 0.55f  => 3,
				       _        => 4
			       };
		}

		private static int HumidityLevel(float v)
		{
			return v switch
			       {
				       // Humidity LEVEL_0..4 band edges
				       < -0.35f => 0,
				       < -0.1f  => 1,
				       < 0.1f   => 2,
				       < 0.3f   => 3,
				       _        => 4
			       };
		}

		/// <summary> Representative biome temperature (mid of the RTF noise-pair table). </summary>
		public static float BiomeTemperature(TerraBiome biome)
		{
			return biome switch
			       {
				       TerraBiome.TropicalRainforest  => 0.375f,
				       TerraBiome.Savanna             => 0.375f,
				       TerraBiome.Desert              => 0.775f,
				       TerraBiome.TemperateRainforest => 0.025f,
				       TerraBiome.TemperateForest     => 0.025f,
				       TerraBiome.Grassland           => -0.15f,
				       TerraBiome.ColdSteppe          => -0.725f,
				       TerraBiome.Steppe              => -0.15f,
				       TerraBiome.Taiga               => -0.3f,
				       TerraBiome.Tundra              => -0.725f,
				       _                              => -0.3f
			       };
		}

		/// <summary> Representative biome moisture (mid of the RTF noise-pair table). </summary>
		public static float BiomeMoisture(TerraBiome biome)
		{
			return biome switch
			       {
				       TerraBiome.TropicalRainforest  => 0.4f,
				       TerraBiome.Savanna             => -0.45f,
				       TerraBiome.Desert              => -0.65f,
				       TerraBiome.TemperateRainforest => 0.65f,
				       TerraBiome.TemperateForest     => 0.1f,
				       TerraBiome.Grassland           => -0.45f,
				       TerraBiome.ColdSteppe          => 0.65f,
				       TerraBiome.Steppe              => -0.1f,
				       TerraBiome.Taiga               => 0.3f,
				       TerraBiome.Tundra              => -0.2f,
				       _                              => 0.65f
			       };
		}
	}
}
