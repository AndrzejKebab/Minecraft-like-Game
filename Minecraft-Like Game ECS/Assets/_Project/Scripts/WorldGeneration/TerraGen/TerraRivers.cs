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

			// rivers only run through low/mid lands — without a network solver the
			// per-column water levels can't stay consistent on steep mountainsides
			// (mountain valleys come from the droplet erosion filter instead)
			var eAlt = (cell.Height - levels.Water) / (1f - levels.Water);
			strength *= 1f - math.saturate((eAlt - 0.30f) / 0.20f);

			if (strength <= 0f)
			{
				cell.RiverMask = 1f;
				return;
			}

			var carveMod = strength;

			var valleyWidth = s.RiverValleyWidth;
			var bankWidth   = s.RiverBankWidth;
			var bedWidth    = s.RiverBedWidth;

			cell.RiverMask = math.saturate(e / valleyWidth);

			if (e >= valleyWidth) return;

			// carving works in REAL block units, converted through the hypsometric
			// curve at the local height (NormForBlocks) so valleys are properly
			// incised regardless of altitude
			var h0 = cell.Height;

			// ── valley: pull terrain down toward the water line ────────────────
			var valleyAlpha = 1f - e / valleyWidth;
			valleyAlpha = TerraNoise.InterpQuintic(valleyAlpha);
			var valleyCarve = valleyAlpha * levels.NormForBlocks(h0, s.RiverValleyDepth) * carveMod;

			var banks = math.max(levels.Water + levels.BlocksAboveSea(1f), h0 - valleyCarve);

			// local water surface sits ~2 blocks below the banks, clamped to sea
			var waterSurface = math.max(levels.Water, banks - levels.NormForBlocks(banks, 2f));

			if (e < bankWidth && carveMod > 0.25f)
			{
				// ── channel: banks slope down into a bed below the water line ───
				var t = math.saturate((e - bedWidth) / math.max(1e-6f, bankWidth - bedWidth));
				t = TerraNoise.InterpHermite(t);
				var bed     = waterSurface - levels.NormForBlocks(waterSurface, s.RiverBedDepth);
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
