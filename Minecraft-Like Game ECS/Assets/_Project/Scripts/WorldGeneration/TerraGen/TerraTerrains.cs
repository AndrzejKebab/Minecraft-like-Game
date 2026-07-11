using Unity.Burst;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary> Result of evaluating one terrain populator at a point. </summary>
	public struct TerrainSample
	{
		public float        Height; // normalised
		public float        Erosion;
		public float        Weirdness;
		public TerraTerrain Terrain;
	}

	/// <summary>
	///     Port of RTF Populators + RegionModule + RegionSelector/RegionLerper + Blender.
	///     Every RTF CellPopulator becomes a static function returning a TerrainSample;
	///     blending helpers combine them exactly like RTF's lerper populators do
	///     (height lerps, categorical fields come from the dominant side).
	/// </summary>
	[BurstCompile]
	public static class TerraTerrains
	{
		// biome-parameter constants (approximate mids of RTF Erosion/Weirdness slices)
		private const float EROSION_L1 = -0.6f;
		private const float EROSION_L2 = -0.3f;
		private const float EROSION_L3 = -0.1f;
		private const float EROSION_L4 = 0.15f;
		private const float EROSION_L5 = 0.35f;
		private const float DEFAULT_EROSION   = EROSION_L4;
		private const float DEFAULT_WEIRDNESS = 0.33f; // MID_SLICE_NORMAL_DESCENDING mid
		private const float WEIRDNESS_LOW_SLICE = 0.55f;

		// seed offsets per populator family
		private const int SEED_REGION     = 789_124;
		private const int SEED_TERRAIN    = 12_000;
		private const int SEED_MOUNTAIN   = 34_000;
		private const int SEED_OCEAN      = 56_000;
		private const int SEED_MTN_SHAPE  = 78_000;

		private const int   REGION_WARP_SCALE    = 400;
		private const int   REGION_WARP_STRENGTH = 200;

		// ── Region voronoi (RegionModule) ────────────────────────────────────

		public static void ApplyRegion(ref TerraCell cell, float x, float z, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_REGION + 7;
			var frequency = 1f / s.TerrainRegionSize;

			// domain warp: two simplex(400) fields, strength 200
			var wx = x;
			var wz = z;
			var ox = TerraNoise.PerlinSigned(wx, wz, seed + 101, 1f / REGION_WARP_SCALE, 1) * REGION_WARP_STRENGTH;
			var oz = TerraNoise.PerlinSigned(wx, wz, seed + 202, 1f / REGION_WARP_SCALE, 1) * REGION_WARP_STRENGTH;
			var px = (x + ox) * frequency;
			var pz = (z + oz) * frequency;

			var xi = TerraNoise.Floor(px);
			var zi = TerraNoise.Floor(pz);
			var cellX = 0;
			var cellZ = 0;
			var edgeDistance  = float.MaxValue;
			var edgeDistance2 = float.MaxValue;
			var nearestVec    = new float2(0.5f, 0.5f);
			for (var dz = -1; dz <= 1; dz++)
			for (var dx = -1; dx <= 1; dx++)
			{
				var    cx  = xi + dx;
				var    cz  = zi + dz;
				float2 vec = TerraNoise.CellVec(seed, cx, cz);
				var    vx  = cx + vec.x * 0.7f;
				var    vz  = cz + vec.y * 0.7f;
				var    d   = TerraNoise.DistApply(TerraNoise.DistFunc.Natural, vx - px, vz - pz);
				if (d < edgeDistance)
				{
					edgeDistance2 = edgeDistance;
					edgeDistance  = d;
					cellX         = cx;
					cellZ         = cz;
					nearestVec    = vec;
				}
				else if (d < edgeDistance2)
				{
					edgeDistance2 = d;
				}
			}

			cell.TerrainRegionId   = 0.5f + TerraNoise.ValCoord2D(seed, cellX, cellZ) * 0.5f;
			cell.TerrainRegionEdge = RegionEdgeValue(edgeDistance, edgeDistance2);
			cell.TerrainRegionCenter = new float2(
				(cellX + nearestVec.x * 0.7f) / frequency,
				(cellZ + nearestVec.y * 0.7f) / frequency);
		}

		private static float RegionEdgeValue(float distance, float distance2)
		{
			// EdgeFunction.DISTANCE_2_DIV then pow 1.5, edge range [0, 0.5]
			var value     = distance / distance2 - 1f; // [-1, 0]
			var edgeValue = 1f - TerraNoise.Map(value, -1f, 0f, 1f);
			edgeValue = math.pow(edgeValue, 1.5f);
			if (edgeValue < 0f) return 0f;
			if (edgeValue > 0.5f) return 1f;
			return edgeValue / 0.5f;
		}

		// ── Populators (Populators.java ports) ───────────────────────────────

		private static void Warp2(ref float x, ref float z, int seedA, int seedB,
		                          float scale, int octaves, float lacunarity, float strength)
		{
			var freq = 1f / math.max(1f, scale);
			var ox   = TerraNoise.PerlinSigned(x, z, seedA, freq, octaves, lacunarity);
			var oz   = TerraNoise.PerlinSigned(x, z, seedB, freq, octaves, lacunarity);
			x += ox * strength;
			z += oz * strength;
		}

		public static TerrainSample Steppe(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 100;
			const int scaleH = 250;

			var erosion = TerraNoise.Alpha(TerraNoise.Perlin(x, z, seed, 1f / (scaleH * 2), 3, 3.75f), 0.45f);

			var wx = x;
			var wz = z;
			Warp2(ref wx, ref wz, seed + 1, seed + 2, scaleH / 4f, 3, 3f, scaleH / 4f);
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 4, 256, 1, 200f);
			var height = TerraNoise.Perlin(wx, wz, seed + 3, 1f / scaleH, 1) * erosion;
			height = height * 0.08f - 0.02f;
			return Make(TerraTerrain.Flats, ground, height * s.GlobalVerticalScale,
			            DEFAULT_EROSION, DEFAULT_WEIRDNESS);
		}

		public static TerrainSample Plains(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 200;
			const int scaleH = 250;

			var erosion = TerraNoise.Alpha(TerraNoise.Perlin(x, z, seed, 1f / (scaleH * 2), 3, 3.75f), 0.45f);

			var wx = x;
			var wz = z;
			Warp2(ref wx, ref wz, seed + 1, seed + 2, scaleH / 4f, 3, 3.5f, scaleH / 4f);
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 4, 256, 1, 256f);
			var height = TerraNoise.Perlin(wx, wz, seed + 3, 1f / scaleH, 1) * erosion;
			height = height * 0.15f * s.GlobalVerticalScale - 0.02f;
			return Make(TerraTerrain.Flats, ground, height, DEFAULT_EROSION, DEFAULT_WEIRDNESS);
		}

		public static TerrainSample Plateau(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 300;

			var vx = x;
			var vz = z;
			TerraNoise.WarpPerlin(ref vx, ref vz, seed + 1, 100, 1, 150f);
			TerraNoise.WarpPerlin(ref vx, ref vz, seed + 2, 20, 1, 15f);
			var valley = 1f - TerraNoise.Ridge(vx, vz, seed, 1f / 500f, 1);

			var tx = x;
			var tz = z;
			TerraNoise.WarpPerlin(ref tx, ref tz, seed + 4, 300, 1, 150f);
			TerraNoise.WarpPerlin(ref tx, ref tz, seed + 5, 40, 2, 20f);
			var top = TerraNoise.Ridge(tx, tz, seed + 3, 1f / 150f, 3, 2.45f) * 0.15f;

			var valleyScaler = TerraNoise.Map(valley, 0.02f, 0.1f, 0.08f);
			top *= valleyScaler;

			var sx = x;
			var sz = z;
			TerraNoise.WarpPerlin(ref sx, ref sz, seed + 7, 40, 2, 20f);
			var surface = TerraNoise.Perlin(sx, sz, seed + 6, 1f / 20f, 3) * 0.05f;

			var cubic = TerraNoise.Perlin(x, z, seed + 8, 1f / 500f, 1) * 0.6f + 0.3f;

			var valleyBase = valley * cubic + top;
			var height = TerraNoise.Steps(valleyBase, 4, 0.15f, 0.55f, TerraNoise.Interp.Curve3);
			height = (height + surface) * 0.475f * s.GlobalVerticalScale;

			var weirdness = TerraNoise.Map(valleyBase, 0f, 0.415f, 0.415f);
			weirdness = TerraNoise.MapRange(weirdness, WEIRDNESS_LOW_SLICE, -0.42f, 0f, 1f);
			return Make(TerraTerrain.Plateau, ground, height, EROSION_L3, weirdness);
		}

		public static TerrainSample Hills1(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 400;

			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 2, 30, 3, 20f);
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 3, 400, 3, 200f);

			var height = TerraNoise.Perlin(wx, wz, seed, 1f / 200f, 3);
			height *= TerraNoise.Alpha(TerraNoise.Billow(wx, wz, seed + 1, 1f / 400f, 3), 0.5f);
			height *= 0.6f * s.GlobalVerticalScale;
			return Make(TerraTerrain.Hills, ground, height, DEFAULT_EROSION, DEFAULT_WEIRDNESS);
		}

		public static TerrainSample Hills2(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 500;

			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 2, 30, 3, 20f);
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 3, 400, 3, 200f);

			var height = TerraNoise.Perlin(wx, wz, seed, 1f / 128f, 3); // cubic → perlin approx
			height *= TerraNoise.Alpha(TerraNoise.Perlin(wx, wz, seed + 1, 1f / 32f, 4), 0.075f);
			height *= TerraNoise.Alpha(TerraNoise.Ridge(wx, wz, seed + 4, 1f / 512f, 2), 0.8f);
			height *= 0.55f * s.GlobalVerticalScale;
			return Make(TerraTerrain.Hills, ground, height, DEFAULT_EROSION, DEFAULT_WEIRDNESS);
		}

		public static TerrainSample Dales(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 600;

			// the whole hill blend is sampled through one warp (RTF warps the blended result)
			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 3, 300, 1, 100f);

			var hills1 = TerraNoise.PowCurve(TerraNoise.Billow(wx, wz, seed, 1f / 300f, 4, 4f, 0.8f), 0.5f) * 0.75f;
			var hills2 = math.pow(TerraNoise.Billow(wx, wz, seed + 1, 1f / 350f, 3, 4f, 0.8f), 1.25f);

			var selector = TerraNoise.Perlin(wx, wz, seed + 2, 1f / 400f, 1);
			selector = TerraNoise.Map(selector, 0.3f, 0.6f, 0.3f);

			var height = TerraNoise.Blend(selector, hills1, hills2, 0.4f, 0.75f);
			height = math.pow(math.saturate(height), 1.125f);

			var erosion   = TerraNoise.Threshold(selector, EROSION_L2, EROSION_L4, 0.5f);
			var weirdness = math.min(-height, -0.06f);
			return Make(TerraTerrain.Hills, ground, height * 0.4f * s.GlobalVerticalScale, erosion, weirdness);
		}

		public static TerrainSample Badlands(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 700;

			var mask = TerraNoise.Map(TerraNoise.Perlin(x, z, seed, 1f / 270f, 3), 0.35f, 0.65f, 0.3f);

			var hx = x;
			var hz = z;
			TerraNoise.WarpPerlin(ref hx, ref hz, seed + 2, 400, 2, 100f);
			TerraNoise.WarpPerlin(ref hx, ref hz, seed + 3, 18, 1, 20f);
			var hills = TerraNoise.Ridge(hx, hz, seed + 1, 1f / 275f, 4) * mask;

			const float modulation = 0.4f;
			const float alpha      = 1f - modulation;

			var mx = x;
			var mz = z;
			TerraNoise.WarpPerlin(ref mx, ref mz, seed + 4, 100, 1, 50f);
			var mod1 = TerraNoise.Ridge(mx, mz, seed + 1, 1f / 275f, 4) * mask * modulation;

			var lowFreq  = TerraNoise.Steps(hills, 4, 0.6f, 0.7f) * alpha + mod1;
			var highFreq = TerraNoise.Steps(hills, 10, 0.6f, 0.7f) * alpha + mod1;
			var detail   = TerraNoise.Alpha(lowFreq + highFreq, 0.5f);

			var scaler = TerraNoise.Perlin(x, z, seed + 5, 1f / 200f, 3) * modulation;
			var mod2   = hills * scaler;

			var shape = TerraNoise.Steps(hills, 4, 0.65f, 0.75f, TerraNoise.Interp.Curve3) * alpha + mod2;
			shape *= alpha;

			var height = shape * detail * 0.55f + 0.025f;
			return Make(TerraTerrain.Badlands, ground, height * s.GlobalVerticalScale,
			            DEFAULT_EROSION, DEFAULT_WEIRDNESS);
		}

		public static TerrainSample Torridonian(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_TERRAIN + 800;

			var px = x;
			var pz = z;
			TerraNoise.WarpPerlin(ref px, ref pz, seed + 1, 300, 1, 150f);
			TerraNoise.WarpPerlin(ref px, ref pz, seed + 2, 20, 1, 40f);
			var plains = TerraNoise.Perlin(px, pz, seed, 1f / 100f, 3) * 0.15f;

			var hx = x;
			var hz = z;
			TerraNoise.WarpPerlin(ref hx, ref hz, seed + 4, 300, 1, 200f);
			TerraNoise.WarpPerlin(ref hx, ref hz, seed + 5, 20, 2, 20f);
			var hills = TerraNoise.Boost(TerraNoise.Perlin(hx, hz, seed + 3, 1f / 150f, 4));

			var selector = TerraNoise.Perlin(x, z, seed + 6, 1f / 200f, 3);

			var modulation = TerraNoise.Perlin(x, z, seed + 7, 1f / 120f, 1) * 0.25f;
			var mask       = TerraNoise.Perlin(x, z, seed + 8, 1f / 200f, 1) * 0.5f + 0.5f;

			var blend = TerraNoise.Blend(selector, plains, hills, 0.6f, 0.6f);
			blend = TerraNoise.AdvancedTerrace(blend, modulation, mask, 0.5f, 0f, 0.3f, 6);

			var height    = TerraNoise.Boost(blend) * 0.5f;
			var weirdness = math.min(-blend, WEIRDNESS_LOW_SLICE - 0.01f);
			return Make(TerraTerrain.Hills, ground, height * s.GlobalVerticalScale, EROSION_L5, weirdness);
		}

		// ── Mountains ────────────────────────────────────────────────────────

		private const int   MOUNTAINS_H  = 610;
		private const float MOUNTAINS_V  = 1.3f;
		private const int   MOUNTAINS3_H = 600;
		private const float MOUNTAINS3_V = 1.185f;

		private static float MountainRidgeHeight(float x, float z, int seed, float scaleH,
		                                         in TerraGenSettings s)
		{
			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 2, 350, 1, 150f);

			var height = TerraNoise.Ridge(wx, wz, seed, 1f / scaleH, 4, 2.35f, 1.15f);
			height *= TerraNoise.Alpha(TerraNoise.Perlin(wx, wz, seed + 1, 1f / 24f, 4), 0.075f);

			if (s.FancyMountains)
				height = TerraNoise.ErodedNoise(x, z, seed + 3, 2, 0.65f, 128f, 0.15f, 3.1f, 0.8f,
				                                height, 1f / scaleH, seed, 4, 2.35f, 1.15f);
			return height;
		}

		public static TerrainSample Mountains1(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed   = s.Seed + SEED_MOUNTAIN + 100;
			var height = MountainRidgeHeight(x, z, seed, MOUNTAINS_H, in s);
			return Make(TerraTerrain.Mountains1, ground,
			            height * MOUNTAINS_V * s.GlobalVerticalScale,
			            EROSION_L1, math.min(-height, -0.08f));
		}

		public static TerrainSample MountainChain(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed   = s.Seed + SEED_MOUNTAIN + 200;
			var height = MountainRidgeHeight(x, z, seed, MOUNTAINS_H * 2.25f, in s);
			return Make(TerraTerrain.MountainChain, ground,
			            height * MOUNTAINS_V * s.GlobalVerticalScale,
			            EROSION_L1, math.min(-height, -0.08f));
		}

		private static float MountainCellBase(float x, float z, int seed, float cellScale)
		{
			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 1, 200, 2, 100f);
			var cell = TerraNoise.WorleyEdge(wx, wz, seed, 1f / cellScale,
			                                 TerraNoise.EdgeFunc.Distance2, TerraNoise.DistFunc.Euclidean);
			cell = math.saturate(cell * 1.2f);

			var blur    = TerraNoise.Alpha(TerraNoise.Perlin(wx, wz, seed + 2, 1f / 10f, 1), 0.025f);
			var surface = TerraNoise.Alpha(TerraNoise.Ridge(wx, wz, seed + 3, 1f / 125f, 4), 0.37f);
			return math.pow(math.saturate(cell * blur * surface), 1.1f);
		}

		public static TerrainSample Mountains2(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed   = s.Seed + SEED_MOUNTAIN + 300;
			var height = MountainCellBase(x, z, seed, 360f);
			if (s.FancyMountains)
				height = TerraNoise.ErodedNoise(x, z, seed + 4, 2, 0.65f, 128f, 0.15f, 3.1f, 0.8f,
				                                height, 1f / 360f, seed, 4, 2f, 0.5f);
			return Make(TerraTerrain.Mountains2, ground,
			            height * 0.645f * s.GlobalVerticalScale,
			            EROSION_L2, math.min(-height, -0.08f));
		}

		public static TerrainSample Mountains3(float x, float z, float ground, in TerraGenSettings s)
		{
			var seed      = s.Seed + SEED_MOUNTAIN + 400;
			var mountains = MountainCellBase(x, z, seed, MOUNTAINS3_H);

			var modulation = TerraNoise.Perlin(x, z, seed + 5, 1f / 50f, 1) * 0.5f;
			var mask       = TerraNoise.Map(TerraNoise.Perlin(x, z, seed + 6, 1f / 100f, 1), 0.5f, 0.95f, 0.45f);

			var height = TerraNoise.AdvancedTerrace(mountains, modulation, mask, 0.45f, 0.2f, 0.45f, 24);
			if (s.FancyMountains)
				height = TerraNoise.ErodedNoise(x, z, seed + 7, 2, 0.65f, 128f, 0.15f, 3.1f, 0.8f,
				                                height, 1f / MOUNTAINS3_H, seed, 4, 2f, 0.5f);
			return Make(TerraTerrain.Mountains3, ground,
			            height * MOUNTAINS3_V * s.GlobalVerticalScale,
			            EROSION_L1, math.min(-height, -0.08f));
		}

		// ── Oceans (Populators.makeDeepOcean / makeShallowOcean / makeCoast) ──

		public static TerrainSample DeepOcean(float x, float z, in TerraLevels levels, in TerraGenSettings s)
		{
			var seed     = s.Seed + SEED_OCEAN;
			var seaLevel = levels.Water;

			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 6, 50, 2, 50f);

			var hills = TerraNoise.Perlin(wx, wz, seed, 1f / 150f, 3) * (seaLevel * 0.7f)
			            + TerraNoise.Perlin(wx, wz, seed + 1, 1f / 200f, 1) * (seaLevel * 0.2f);

			var canyons = (1f - TerraNoise.PowCurve(TerraNoise.Perlin(wx, wz, seed + 2, 1f / 150f, 4), 0.2f))
			              * (seaLevel * 0.7f)
			              + TerraNoise.Perlin(wx, wz, seed + 3, 1f / 170f, 1) * (seaLevel * 0.15f);

			var selector = TerraNoise.Perlin(wx, wz, seed + 4, 1f / 500f, 1);
			var height   = TerraNoise.Blend(selector, hills, canyons, 0.6f, 0.65f);

			return new TerrainSample
			       {
				       Height    = math.max(height, 0f),
				       Erosion   = -1.1f,
				       Weirdness = -1.1f,
				       Terrain   = TerraTerrain.DeepOcean
			       };
		}

		public static TerrainSample ShallowOcean(in TerraLevels levels)
		{
			return new TerrainSample
			       {
				       Height    = levels.WaterPlus(-7),
				       Erosion   = -1.1f,
				       Weirdness = -1.1f,
				       Terrain   = TerraTerrain.ShallowOcean
			       };
		}

		public static TerrainSample Coast(in TerraLevels levels)
		{
			return new TerrainSample
			       {
				       Height    = levels.Water,
				       Erosion   = -1.1f,
				       Weirdness = -1.1f,
				       Terrain   = TerraTerrain.Coast
			       };
		}

		// ── Selection + blending ─────────────────────────────────────────────

		/// <summary>
		///     RegionSelector — weighted list flattened at compile time.
		///     Index layout ≈ RTF TerrainProvider default weights.
		/// </summary>
		public static TerrainSample SelectRegionTerrain(float identity, float x, float z,
		                                                float ground, in TerraGenSettings s)
		{
			const int maxIndex = 13;
			var index = math.clamp(TerraNoise.Round(identity * maxIndex), 0, maxIndex);
			switch (index)
			{
				case 0:  return Steppe(x, z, ground, in s);
				case 1:  return Plains(x, z, ground, in s);
				case 2:  return Dales(x, z, ground, in s);
				case 3:  return Hills1(x, z, ground, in s);
				case 4:  return Plains(x, z, ground, in s);
				case 5:  return Torridonian(x, z, ground, in s);
				case 6:  return Plateau(x, z, ground, in s);
				case 7:  return Badlands(x, z, ground, in s);
				case 8:  return Hills2(x, z, ground, in s);
				case 9:  return Mountains1(x, z, ground, in s);
				case 10: return Steppe(x, z, ground, in s);
				case 11: return Mountains2(x, z, ground, in s);
				case 12: return Plateau(x, z, ground, in s);
				default: return Mountains3(x, z, ground, in s);
			}
		}

		/// <summary> RTF makeBorder — plains-scaled filler between terrain regions. </summary>
		public static TerrainSample Border(float x, float z, float ground, in TerraGenSettings s)
		{
			return Plains(x, z, ground, in s);
		}

		/// <summary> RegionLerper — blends border ↔ region terrain by region edge. </summary>
		public static TerrainSample LerpRegion(in TerrainSample lower, in TerrainSample upper, float alpha)
		{
			if (alpha <= 0f) return lower;
			if (alpha >= 1f) return upper;
			var result = upper; // categorical fields follow the region terrain
			result.Height    = math.lerp(lower.Height, upper.Height, alpha);
			result.Erosion   = math.lerp(lower.Erosion, upper.Erosion, alpha);
			result.Weirdness = math.lerp(lower.Weirdness, upper.Weirdness, alpha);
			return result;
		}

		/// <summary>
		///     Mountain-chain shape noise (Heightmap.make mountainShape):
		///     worley edge DISTANCE_2_ADD warped, curved, clamped to [0, 0.9], mapped [0,1].
		/// </summary>
		public static float MountainShape(float x, float z, in TerraGenSettings s)
		{
			var seed = s.Seed + SEED_MTN_SHAPE;
			var wx   = x;
			var wz   = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 1, 333, 2, 250f);
			var shape = TerraNoise.WorleyEdge(wx, wz, seed, 1f / (1000f * 2.25f),
			                                  TerraNoise.EdgeFunc.Distance2Add, TerraNoise.DistFunc.Euclidean);
			shape = TerraNoise.InterpHermite(math.saturate(shape));
			shape = math.clamp(shape, 0f, 0.9f);
			return shape / 0.9f;
		}

		/// <summary> Blender — control-noise blend between two populator results. </summary>
		public static TerrainSample BlendTerrain(float select, in TerrainSample lower, in TerrainSample upper,
		                                         float min, float max, float split)
		{
			if (select < min) return lower;
			if (select > max) return upper;
			var alpha  = (select - min) / (max - min);
			var result = select < min + (max - min) * split ? lower : upper;
			result.Height    = math.lerp(lower.Height, upper.Height, alpha);
			result.Erosion   = math.lerp(lower.Erosion, upper.Erosion, alpha);
			result.Weirdness = math.lerp(lower.Weirdness, upper.Weirdness, alpha);
			return result;
		}

		private static TerrainSample Make(TerraTerrain type, float ground, float height,
		                                  float erosion, float weirdness)
		{
			return new TerrainSample
			       {
				       Height    = math.max(ground + height, 0f),
				       Erosion   = erosion,
				       Weirdness = weirdness,
				       Terrain   = type
			       };
		}
	}
}
