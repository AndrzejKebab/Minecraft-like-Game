using Unity.Burst;
using Unity.Collections;
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

			// all carving happens in REAL block units, converted through the
			// hypsometric curve — depths and water levels come out exact
			var hBlocks = levels.ToBlocksF(cell.Height);

			// rivers only run through LOW lands — without a network solver the
			// per-column water levels can't stay consistent on slopes, which turns
			// into hanging water sheets on hillsides (mountain and hill valleys
			// come from the droplet erosion filter instead)
			strength *= 1f - math.saturate((hBlocks - 40f) / 40f);

			if (strength <= 0f || hBlocks <= 0.5f)
			{
				cell.RiverMask = 1f;
				return;
			}

			var carveMod    = strength;
			var valleyWidth = s.RiverValleyWidth;
			var bankWidth   = s.RiverBankWidth;
			var bedWidth    = s.RiverBedWidth;

			cell.RiverMask = math.saturate(e / valleyWidth);

			if (e >= valleyWidth) return;

			// ── valley: carve toward a floor scaled by the LOCAL relief, so
			// lowland rivers stay incised in their terrain instead of bottoming
			// out at sea level and reading like ocean arms
			var valleyDepth = math.min((float)s.RiverValleyDepth, math.max(3f, hBlocks * 0.6f));
			var valleyAlpha = 1f - e / valleyWidth;
			valleyAlpha = TerraNoise.InterpQuintic(valleyAlpha);

			var banksBlocks = hBlocks - valleyAlpha * valleyDepth * carveMod;
			banksBlocks = math.max(banksBlocks, math.min(hBlocks, 1f));

			// water level quantised into 4-block pools so the surface forms flat
			// stretches with occasional small falls, instead of following the
			// banks block-by-block (which reads as terraced water on any slope)
			var waterBlocks = math.max(0f, math.floor((banksBlocks - 2f) / 4f) * 4f);

			if (e < bankWidth && carveMod > 0.4f)
			{
				// ── channel: banks slope down into a bed below the water line ───
				var t = math.saturate((e - bedWidth) / math.max(1e-6f, bankWidth - bedWidth));
				t = TerraNoise.InterpHermite(t);
				var bedBlocks     = waterBlocks - s.RiverBedDepth;
				var channelBlocks = math.lerp(bedBlocks, banksBlocks, t);
				var channel       = levels.FromBlocksF(channelBlocks);
				if (channel < cell.Height) cell.Height = channel;

				var waterSurface = levels.FromBlocksF(waterBlocks);
				if (cell.Height < waterSurface)
				{
					cell.Terrain         = TerraTerrain.River;
					cell.RiverWaterLevel = waterSurface;
				}
			}
			else
			{
				var banks = levels.FromBlocksF(banksBlocks);
				if (banks < cell.Height) cell.Height = banks;
			}
		}

		// max block a river's water surface may drop between two adjacent columns.
		// Steeper steps are impossible for standing water — they render as a
		// vertical wall — so the relaxation ramps them out over several columns.
		private const float MAX_WATER_STEP = 1f;

		/// <summary>
		///     Tile-level water consistency pass (runs in TerraTileGenJob over the
		///     bordered grid, in block units). Per-column water levels, taken from the
		///     nearest reach, can disagree with their surroundings — the surface can
		///     sit above adjacent land (it would pour out) or drop faster than water
		///     physically can between two columns (a vertical wall of water). Both read
		///     as water hanging in the air. Relax the surface until neither happens:
		///
		///     1. spill  — water above an adjacent LAND surface is clamped to it.
		///     2. ramp   — water more than MAX_WATER_STEP above an adjacent RIVER
		///                 column is clamped to neighbour + step, so a steep drop
		///                 spreads into a gentle downhill ramp instead of a wall.
		///
		///     Both rules only ever LOWER water, so a river keeps its natural profile
		///     wherever that profile is already gentle; only walls get ramped. The
		///     rules propagate one column per iteration, so we iterate enough to carry
		///     a low outlet up a long flooded reach. Finally the bed is dropped to stay
		///     just below the settled surface, so the ramped stretch actually holds
		///     flowing water rather than turning into a dry ditch.
		/// </summary>
		public static void SettleWater(in NativeArray<TerraGenCell> cells,
		                               ref NativeArray<float> surfBlocks,
		                               ref NativeArray<float> waterBlocks, int size)
		{
			// enough iterations to ramp out a tall wall across a wide flooded reach
			const int iterations = 24;

			for (var it = 0; it < iterations; it++)
			{
				var changed = false;
				for (var z = 1; z < size - 1; z++)
				for (var x = 1; x < size - 1; x++)
				{
					var i = x + z * size;
					if (cells[i].Terrain != TerraTerrain.River) continue;

					var w = waterBlocks[i];
					for (var n = 0; n < 4; n++)
					{
						var j = Neighbor(i, n, size);
						float limit;
						if (cells[j].Terrain == TerraTerrain.River)
							// downhill ramp: can't stand more than one step over a
							// lower river neighbour — propagates the low outlet upstream
							limit = waterBlocks[j] + MAX_WATER_STEP;
						else
							// spill: submerged land contains up to the sea surface
							limit = math.max(surfBlocks[j], 0f);
						if (limit < w) w = limit;
					}

					if (w < waterBlocks[i])
					{
						waterBlocks[i] = w;
						changed        = true;
					}
				}

				if (!changed) break;
			}

			// keep the bed just under the settled surface so the ramped-down stretch
			// carries water instead of leaving an exposed dry channel (only lowers)
			for (var i = 0; i < size * size; i++)
			{
				if (cells[i].Terrain != TerraTerrain.River) continue;
				var floor = waterBlocks[i] - 1f;
				if (floor < surfBlocks[i]) surfBlocks[i] = floor;
			}
		}

		private static int Neighbor(int index, int direction, int size)
		{
			switch (direction)
			{
				case 0:  return index - 1;
				case 1:  return index + 1;
				case 2:  return index - size;
				default: return index + size;
			}
		}

		private const float BEACH_MAX_HEIGHT = 5f; // blocks above sea a shore may still be sanded
		private const int   BEACH_REACH      = 6;  // sand extends this many blocks in from the water

		/// <summary>
		///     Shoreline sand pass (runs in TerraTileGenJob over the bordered grid). This
		///     is the ONLY beach source: a thin, uniform strip of sand on the land that
		///     actually touches the sea, so an ocean edge reads water → sand → grass
		///     everywhere without the wide beaches a height rule made on flat coasts.
		///     Any land column within BEACH_REACH of an underwater sea column (surface
		///     below sea level, not a river) becomes sand, unless it is a tall cliff.
		///     River banks are left as grass — their wet neighbours are river, not sea.
		/// </summary>
		public static void ShorelineBeach(ref NativeArray<TerraGenCell> cells,
		                                  in NativeArray<float> surfBlocks, int size)
		{
			for (var z = 0; z < size; z++)
			for (var x = 0; x < size; x++)
			{
				var i = x + z * size;
				var c = cells[i];
				if (c.Terrain == TerraTerrain.River) continue;

				var surf = surfBlocks[i];
				if (surf < 0f || surf > BEACH_MAX_HEIGHT) continue; // underwater, or too high (cliff)

				// open sea within reach? (any column whose surface is below sea level)
				var nearSea = false;
				for (var dz = -BEACH_REACH; dz <= BEACH_REACH && !nearSea; dz++)
				for (var dx = -BEACH_REACH; dx <= BEACH_REACH; dx++)
				{
					var nx = x + dx;
					var nz = z + dz;
					if (nx < 0 || nx >= size || nz < 0 || nz >= size) continue;
					var j = nx + nz * size;
					if (surfBlocks[j] < 0f && cells[j].Terrain != TerraTerrain.River)
					{
						nearSea = true;
						break;
					}
				}

				if (!nearSea) continue;
				c.Terrain = TerraTerrain.Beach;
				cells[i]  = c;
			}
		}
	}
}
