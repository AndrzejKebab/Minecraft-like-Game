using Unity.Burst;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     River valleys + channels.
	///     RTF generates explicit river networks per continent (Rivermap / RiverGenerator)
	///     — object graphs of line segments that don't translate to Burst jobs directly.
	///     This module reproduces the visual result with a continuous function instead:
	///     rivers follow the edges of a warped voronoi "drainage" grid (which naturally
	///     forms connected branching lines), carve a smoothed valley → banks → bed
	///     profile like RTF's RiverCarver, and fade out into the ocean at the coast.
	///     Fully deterministic per column, no caching required.
	/// </summary>
	[BurstCompile]
	public static class TerraRivers
	{
		private const int SEED_RIVER = 700_000;

		public static void Apply(ref TerraCell cell, float x, float z,
		                         in TerraGenSettings s, in TerraLevels levels)
		{
			if (!s.RiversEnabled) return;
			// deep/shallow ocean override rivers (TerrainCategory.overridesRiver)
			if (cell.Terrain == TerraTerrain.DeepOcean || cell.Terrain == TerraTerrain.ShallowOcean)
				return;

			var seed = s.Seed + SEED_RIVER;
			var frequency = 1f / math.max(1, s.RiverScale);

			// warp so the drainage edges meander instead of being straight voronoi walls
			var wx = x;
			var wz = z;
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 1, s.RiverScale * 0.4f, 1, s.RiverScale * 0.11f);
			TerraNoise.WarpPerlin(ref wx, ref wz, seed + 2, s.RiverScale * 0.08f, 2, s.RiverScale * 0.02f);

			var px = wx * frequency;
			var pz = wz * frequency;

			// distance to nearest voronoi edge, scale-invariant:
			// e = (d2 - d1) / (d2 + d1) → 0 exactly on the edge, ~1 at cell centres
			var xi = TerraNoise.Floor(px);
			var zi = TerraNoise.Floor(pz);
			var d1 = float.MaxValue;
			var d2 = float.MaxValue;
			for (var dz = -1; dz <= 1; dz++)
			for (var dx = -1; dx <= 1; dx++)
			{
				var    cx  = xi + dx;
				var    cz  = zi + dz;
				float2 vec = TerraNoise.CellVec(seed, cx, cz);
				var    vx  = cx + vec.x * 0.8f;
				var    vz  = cz + vec.y * 0.8f;
				var    d   = TerraNoise.DistApply(TerraNoise.DistFunc.Euclidean, vx - px, vz - pz);
				if (d < d1)
				{
					d2 = d1;
					d1 = d;
				}
				else if (d < d2)
				{
					d2 = d;
				}
			}

			d1 = math.sqrt(d1);
			d2 = math.sqrt(d2);
			var e = (d2 - d1) / math.max(1e-6f, d2 + d1);

			// river strength: full inland, fading to zero across the coast band so
			// channels open into the sea instead of stopping at a wall
			var strength = math.saturate((cell.ContinentEdge - s.ShallowOcean) /
			                             math.max(1e-6f, s.Coast - s.ShallowOcean));
			if (strength <= 0f)
			{
				cell.RiverMask = 1f;
				return;
			}

			// mountains resist carving a little (no proper uplift data here)
			var altitude  = math.saturate((cell.Height - levels.Ground) / math.max(1e-6f, 0.7f - levels.Ground));
			var carveMod  = strength * (1f - altitude * 0.5f);

			var valleyWidth = s.RiverValleyWidth;
			var bankWidth   = s.RiverBankWidth;
			var bedWidth    = s.RiverBedWidth;

			cell.RiverMask = math.saturate(e / valleyWidth);

			if (e >= valleyWidth) return;

			// ── valley: pull terrain down toward the water line ────────────────
			var valleyAlpha = 1f - e / valleyWidth;
			valleyAlpha = TerraNoise.InterpQuintic(valleyAlpha);
			var valleyCarve = valleyAlpha * levels.Scale(s.RiverValleyDepth) * carveMod;

			var banks = math.max(levels.WaterPlus(1), cell.Height - valleyCarve);

			// local water surface follows the banks, clamped to sea level
			var waterSurface = math.max(levels.Water, banks - levels.Scale(2));

			if (e < bankWidth && carveMod > 0.25f)
			{
				// ── channel: banks slope down into a bed below the water line ───
				var t = math.saturate((e - bedWidth) / math.max(1e-6f, bankWidth - bedWidth));
				t = TerraNoise.InterpHermite(t);
				var bed     = waterSurface - levels.Scale(s.RiverBedDepth);
				var channel = math.lerp(bed, banks, t);
				if (channel < cell.Height) cell.Height = channel;

				if (cell.Height < waterSurface)
				{
					cell.Terrain         = TerraTerrain.River;
					cell.RiverWaterLevel = waterSurface;
				}
			}
			else if (banks < cell.Height)
			{
				cell.Height = banks;
			}
		}
	}
}
