using System;
using Unity.Entities;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Blittable generator configuration — the equivalent of RTF's Preset
	///     (WorldSettings + TerrainSettings + ClimateSettings + RiverSettings).
	///     Lives in a singleton component and is passed by value into Burst jobs.
	///     Defaults mirror RTF's default preset where a counterpart exists.
	/// </summary>
	[Serializable]
	public struct TerraGenSettings : IComponentData
	{
		public int Seed;

		// ── Vertical mapping (sea level is ALWAYS world Y 0) ─────────────────
		public int OceanDepth;     // blocks from sea down to the deepest ocean floor
		public int MountainHeight; // typical big-mountain elevation; rare peaks ~2×

		// ── Continent (RTF WorldSettings.Continent) ──────────────────────────
		public int   ContinentScale;        // default 3000
		public float ContinentJitter;       // default 0.7
		public float ContinentSizeVariance; // default 0.25
		public int   ContinentNoiseOctaves; // default 5
		public float ContinentNoiseGain;    // default 0.26
		public float ContinentNoiseLacunarity; // default 4.33

		// ── Control points (RTF WorldSettings.ControlPoints) ─────────────────
		// narrower coastal band than RTF's defaults so land doesn't spend
		// kilometres blended toward sea level
		public float DeepOcean;    // default 0.1
		public float ShallowOcean; // default 0.32
		public float Beach;        // default 0.40
		public float Coast;        // default 0.45
		public float Inland;       // default 0.50

		// ── Terrain regions (RTF TerrainSettings.General) ────────────────────
		public int   TerrainRegionSize;     // default 1200
		public float GlobalVerticalScale;   // default 0.985
		public float GlobalHorizontalScale; // default 0.85 (terrainFrequency = 1/scale)
		public bool  FancyMountains;        // functional-erosion mountains, default true

		// ── Rivers (simplified network — see TerraRivers) ────────────────────
		public bool  RiversEnabled;
		public int   RiverScale;       // voronoi scale of the drainage network, default 1000
		public float RiverValleyWidth; // fraction of edge-distance forming the valley, default 0.24
		public float RiverBankWidth;   // default 0.025
		public float RiverBedWidth;    // default 0.010
		public int   RiverValleyDepth; // real blocks of valley carve at the banks, default 16
		public int   RiverBedDepth;    // real blocks below the local water surface, default 4

		// ── Filters (RTF FilterSettings — applied per tile) ──────────────────
		public bool  ErosionEnabled;
		public int   ErosionDropletsPerChunk; // default 135
		public int   ErosionDropletLifetime;  // default 12
		public float ErosionDropletVolume;    // default 0.7
		public float ErosionDropletVelocity;  // default 0.7
		public float ErosionRate;             // default 0.5
		public float ErosionDepositRate;      // default 0.5
		public int   SmoothingIterations;     // default 1
		public float SmoothingRadius;         // default 1.8
		public float SmoothingRate;           // default 0.9

		// ── Climate (RTF ClimateSettings) ────────────────────────────────────
		public int   BiomeSize;         // default 800
		public int   BiomeWarpScale;    // default 150
		public float BiomeWarpStrength; // default 80
		public float TemperatureScale;  // default 4 (latitude band size, in biome cells)
		public int   TemperatureFalloff; // sin^power, default 2
		public float MoistureScale;     // default 1.0
		public int   MoistureFalloff;   // default 1

		public static TerraGenSettings Default(int seed)
		{
			return new TerraGenSettings
			       {
				       Seed = seed,

				       OceanDepth     = 256,
				       MountainHeight = 512,

				       ContinentScale           = 3000,
				       ContinentJitter          = 0.7f,
				       ContinentSizeVariance    = 0.25f,
				       ContinentNoiseOctaves    = 5,
				       ContinentNoiseGain       = 0.26f,
				       ContinentNoiseLacunarity = 4.33f,

				       DeepOcean    = 0.1f,
				       ShallowOcean = 0.32f,
				       Beach        = 0.40f,
				       Coast        = 0.45f,
				       Inland       = 0.50f,

				       TerrainRegionSize     = 1200,
				       GlobalVerticalScale   = 0.985f,
				       GlobalHorizontalScale = 0.85f,
				       FancyMountains        = true,

				       RiversEnabled    = true,
				       RiverScale       = 1000,
				       RiverValleyWidth = 0.24f,
				       RiverBankWidth   = 0.025f,
				       RiverBedWidth    = 0.010f,
				       RiverValleyDepth = 16,
				       RiverBedDepth    = 4,

				       ErosionEnabled          = true,
				       ErosionDropletsPerChunk = 135,
				       ErosionDropletLifetime  = 12,
				       ErosionDropletVolume    = 0.7f,
				       ErosionDropletVelocity  = 0.7f,
				       ErosionRate             = 0.5f,
				       ErosionDepositRate      = 0.5f,
				       SmoothingIterations     = 1,
				       SmoothingRadius         = 1.8f,
				       SmoothingRate           = 0.9f,

				       BiomeSize          = 800,
				       BiomeWarpScale     = 150,
				       BiomeWarpStrength  = 80f,
				       TemperatureScale   = 4f,
				       TemperatureFalloff = 2,
				       MoistureScale      = 1f,
				       MoistureFalloff    = 1
			       };
		}
	}
}
