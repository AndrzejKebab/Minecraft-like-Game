using Unity.Burst;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Port of RTF AdvancedContinentGenerator + AbstractContinent.
	///     Voronoi tectonic plates: each cell is a continent, the edge value (distance
	///     to the nearest plate boundary, remapped) drives the ocean→coast→inland blend.
	///     Warp + cliff/bay noise shape the coastline.
	/// </summary>
	[BurstCompile]
	public static class TerraContinent
	{
		// seed offsets (RTF derives these from Seed.next() call order)
		private const int SEED_CELLS    = 0;
		private const int SEED_SKIP     = 1;
		private const int SEED_VARIANCE = 2;
		private const int SEED_WARP_X   = 3;
		private const int SEED_WARP_Z   = 4;
		private const int SEED_CLIFF    = 5;
		private const int SEED_BAY      = 6;

		public static void Apply(ref TerraCell cell, float x, float y, in TerraGenSettings s)
		{
			var baseSeed      = s.Seed + 1_000_000;
			var tectonicScale = s.ContinentScale * 4;
			var frequency     = 1f / tectonicScale;

			// domain warp (createWarp: scale = tectonic*0.225, strength = tectonic*0.33)
			var warpScale    = TerraNoise.Round(tectonicScale * 0.225f);
			var warpStrength = TerraNoise.Round(tectonicScale * 0.33f);
			var wx = x + TerraNoise.PerlinSigned(x, y, baseSeed + SEED_WARP_X, 1f / warpScale,
			                                     s.ContinentNoiseOctaves, s.ContinentNoiseLacunarity,
			                                     s.ContinentNoiseGain) * warpStrength;
			var wy = y + TerraNoise.PerlinSigned(x, y, baseSeed + SEED_WARP_Z, 1f / warpScale,
			                                     s.ContinentNoiseOctaves, s.ContinentNoiseLacunarity,
			                                     s.ContinentNoiseGain) * warpStrength;

			var px = wx * frequency;
			var py = wy * frequency;

			var xi = TerraNoise.Floor(px);
			var yi = TerraNoise.Floor(py);

			// pass 1: nearest plate centre
			var cellX      = xi;
			var cellY      = yi;
			var cellPointX = px;
			var cellPointY = py;
			var nearest    = float.MaxValue;
			for (var cy = yi - 1; cy <= yi + 1; cy++)
			for (var cx = xi - 1; cx <= xi + 1; cx++)
			{
				float2 vec   = TerraNoise.CellVec(baseSeed + SEED_CELLS, cx, cy);
				var    ppx   = cx + vec.x * s.ContinentJitter;
				var    ppy   = cy + vec.y * s.ContinentJitter;
				var    dist2 = DistSq(px, py, ppx, ppy);
				if (!(dist2 < nearest)) continue;
				cellPointX = ppx;
				cellPointY = ppy;
				cellX      = cx;
				cellY      = cy;
				nearest    = dist2;
			}

			// pass 2: distance to the plate boundary (perpendicular bisector of the
			// segment to each neighbouring plate centre)
			nearest = float.MaxValue;
			var sumX = 0f;
			var sumY = 0f;
			for (var cy = cellY - 1; cy <= cellY + 1; cy++)
			for (var cx = cellX - 1; cx <= cellX + 1; cx++)
			{
				if (cx == cellX && cy == cellY) continue;
				float2 vec = TerraNoise.CellVec(baseSeed + SEED_CELLS, cx, cy);
				var    ppx = cx + vec.x * s.ContinentJitter;
				var    ppy = cy + vec.y * s.ContinentJitter;
				sumX += ppx;
				sumY += ppy;
				var dist = BorderDistance(px, py, cellPointX, cellPointY, ppx, ppy);
				if (dist < nearest) nearest = dist;
			}

			cell.ContinentDistance = math.sqrt(nearest);
			// corrected continent centre (getCorrectedContinentCenter, CENTER_CORRECTION 0.35)
			cell.ContinentCenter = new int2(
				(int)(math.lerp(cellPointX, sumX / 8f, 0.35f) / frequency),
				(int)(math.lerp(cellPointY, sumY / 8f, 0.35f) / frequency));

			if (ShouldSkip(baseSeed, cellX, cellY)) return; // ocean plate: edge stays 0

			cell.ContinentId   = TerraNoise.CellValue01(baseSeed + SEED_CELLS, cellX, cellY);
			cell.ContinentEdge = DistanceValue(px, py, cellX, cellY, nearest, baseSeed, frequency, in s);
		}

		/// <summary> Continent edge value only (ClimateModule.getLandValue). </summary>
		public static float GetEdgeValue(float x, float y, in TerraGenSettings s)
		{
			TerraCell cell = TerraCell.Default();
			Apply(ref cell, x, y, in s);
			return cell.ContinentEdge;
		}

		/// <summary> Nearest corrected continent centre for a position. </summary>
		public static int2 GetNearestCenter(float x, float y, in TerraGenSettings s)
		{
			TerraCell cell = TerraCell.Default();
			Apply(ref cell, x, y, in s);
			return cell.ContinentCenter;
		}

		/// <summary>
		///     AbstractContinent.getDistanceToEdge — how far from (cx, cz) along (dx, dz)
		///     until the nearest continent centre changes (the plate boundary).
		/// </summary>
		public static float GetDistanceToEdge(int cx, int cz, float dx, float dz, in TerraGenSettings s)
		{
			var distance = (float)(s.ContinentScale * 4);
			for (var i = 0; i < 10; i++)
			{
				var  x      = cx + dx * distance;
				var  z      = cz + dz * distance;
				int2 center = GetNearestCenter(x, z, in s);
				distance += distance;
				if (center.x == cx && center.y == cz) continue;

				var low  = 0f;
				var high = distance;
				for (var j = 0; j < 50; j++)
				{
					var mid = (low + high) / 2f;
					center = GetNearestCenter(cx + dx * mid, cz + dz * mid, in s);
					if (center.x == cx && center.y == cz) low = mid;
					else high = mid;
					if (high - low < 50f) break;
				}

				return high;
			}

			return distance;
		}

		/// <summary>
		///     AbstractContinent.getDistanceToOcean — how far from (cx, cz) along
		///     (dx, dz) until the coastline (edge value drops to shallow ocean).
		/// </summary>
		public static float GetDistanceToOcean(int cx, int cz, float dx, float dz, in TerraGenSettings s)
		{
			var high = GetDistanceToEdge(cx, cz, dx, dz, in s);
			var low  = 0f;
			for (var i = 0; i < 50; i++)
			{
				var mid  = (low + high) / 2f;
				var edge = GetEdgeValue(cx + dx * mid, cz + dz * mid, in s);
				if (edge > s.ShallowOcean) low = mid;
				else high = mid;
				if (high - low < 10f) break;
			}

			return high;
		}

		private static bool ShouldSkip(int baseSeed, int cellX, int cellY)
		{
			// RTF continentSkipping (random ocean plates); default preset disables it.
			// Enable by comparing CellValue01(baseSeed + SEED_SKIP, ...) to a threshold.
			return false;
		}

		private static float DistanceValue(float x, float y, int cellX, int cellY, float distance,
		                                   int baseSeed, float frequency, in TerraGenSettings s)
		{
			// continent size variance
			if (s.ContinentSizeVariance > 0f && !(cellX == 0 && cellY == 0))
			{
				var sizeValue    = TerraNoise.CellValue01(baseSeed + SEED_VARIANCE, cellX, cellY);
				var sizeModifier = TerraNoise.Map(sizeValue, 0f, s.ContinentSizeVariance, s.ContinentSizeVariance);
				distance *= sizeModifier;
			}

			distance = math.sqrt(distance);
			distance = TerraNoise.Map(distance, 0.05f, 0.25f, 0.2f);
			distance = CoastalDistanceValue(x, y, distance, baseSeed, frequency, in s);
			if (distance < s.Inland && distance >= s.ShallowOcean)
				distance = CoastalDistanceValue(x, y, distance, baseSeed, frequency, in s);
			return distance;
		}

		private static float CoastalDistanceValue(float x, float y, float distance,
		                                          int baseSeed, float frequency, in TerraGenSettings s)
		{
			if (!(distance > s.ShallowOcean) || !(distance < s.Inland)) return distance;

			// x,y here are already in warped cell space; RTF's cliff/bay noise runs at
			// world frequency, so scale back up
			var worldX = x / frequency;
			var worldY = y / frequency;

			// cliffNoise: simplex(continentScale/2, 2 octaves) clamp[0.1,0.25] map[0,1]
			var cliff = TerraNoise.Perlin(worldX, worldY, baseSeed + SEED_CLIFF,
			                              1f / (s.ContinentScale / 2f), 2);
			cliff = TerraNoise.Map(cliff, 0.1f, 0.25f, 0.15f);

			var alpha = distance / s.Inland;
			distance = math.lerp(distance * cliff, distance, alpha);
			if (!(distance < s.ShallowOcean)) return distance;
			// bayNoise: simplex(100, 1) * 0.1 + 0.9
			var bay = TerraNoise.Perlin(worldX, worldY, baseSeed + SEED_BAY, 1f / 100f, 1)
			          * 0.1f + 0.9f;
			distance = s.ShallowOcean * bay;

			return distance;
		}

		private static float DistSq(float x, float y, float px, float py)
		{
			var dx = x - px;
			var dy = y - py;
			return dx * dx + dy * dy;
		}

		/// <summary> Distance² to the perpendicular bisector between plate centres a and b. </summary>
		private static float BorderDistance(float x, float y, float ax, float ay, float bx, float by)
		{
			var mx = (ax + bx) * 0.5f;
			var my = (ay + by) * 0.5f;
			var dx = bx - ax;
			var dy = by - ay;
			// bisector line through midpoint with normal (dx, dy)
			var nx = -dy;
			var v  = ((x - mx) * nx + (y - my) * dx) / (nx * nx + dx * dx);
			var ox = mx + nx * v;
			var oy = my + dx * v;
			return DistSq(x, y, ox, oy);
		}
	}
}
