using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Spawn-point search (the RTF SpawnFinder equivalent): spirals outward from
	///     the origin sampling the heightmap pipeline until it finds a pleasant,
	///     safely-inland column — solid land terrain, above sea level, not inside a
	///     river valley, not up a mountain peak. Deterministic per seed.
	///     Runs as a one-shot Burst job at startup (a few hundred samples at most).
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct TerraSpawnSearchJob : IJob
	{
		private const int STEP      = 48;  // blocks between candidate columns
		private const int MAX_RINGS = 320; // search radius ≈ 15 km

		private const int   MIN_SURFACE_Y  = 2;   // above beaches
		private const int   MAX_SURFACE_Y  = 120; // below serious peaks
		private const float MIN_RIVER_MASK = 0.6f;

		public TerraGenSettings Settings;

		/// <summary> [0].xyz = top solid block of the chosen column, .w = 1 if criteria matched. </summary>
		public NativeArray<int4> Result;

		public void Execute()
		{
			var levels = TerraLevels.Make(Settings.OceanDepth, Settings.MountainHeight);

			for (var ring = 0; ring <= MAX_RINGS; ring++)
			for (var dz = -ring; dz <= ring; dz++)
			for (var dx = -ring; dx <= ring; dx++)
			{
				// perimeter cells only — interior was covered by smaller rings
				if (math.max(math.abs(dx), math.abs(dz)) != ring) continue;

				var x = dx * STEP;
				var z = dz * STEP;
				if (!Evaluate(x, z, in levels, out var surfaceY)) continue;
				Result[0] = new int4(x, surfaceY, z, 1);
				return;
			}

			// pathological settings (e.g. all-ocean world): spawn at the origin on
			// whatever surface is there, clamped above the sea
			TerraHeightmap.Sample(out TerraCell cell, 0f, 0f, in Settings, in levels);
			var fallbackY = math.max(levels.ToBlockY(cell.Height), 0);
			Result[0] = new int4(0, fallbackY, 0, 0);
		}

		private bool Evaluate(float x, float z, in TerraLevels levels, out int surfaceY)
		{
			TerraHeightmap.Sample(out TerraCell cell, x, z, in Settings, in levels);
			surfaceY = levels.ToBlockY(cell.Height);

			// fully inland — keeps oceans, coasts and beaches out
			if (cell.ContinentEdge < Settings.Inland) return false;

			// solid land terrain only (no rivers/lakes/ocean floors)
			if (cell.Terrain < TerraTerrain.Flats) return false;

			// stay out of river valleys — the droplet erosion also concentrates there
			if (cell.RiverMask < MIN_RIVER_MASK) return false;

			return surfaceY >= MIN_SURFACE_Y && surfaceY <= MAX_SURFACE_Y;
		}
	}
}
