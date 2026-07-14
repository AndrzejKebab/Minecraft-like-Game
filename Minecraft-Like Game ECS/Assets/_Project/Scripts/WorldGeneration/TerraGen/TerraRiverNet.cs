using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Deterministic xorshift RNG — Burst-safe replacement for java.util.Random,
	///     so a network is identical regardless of which tile requests it (seamless
	///     across tile boundaries).
	/// </summary>
	public struct TerraRng
	{
		private uint _s;

		public TerraRng(uint seed)
		{
			_s = seed == 0u ? 0x9E3779B9u : seed;
			NextUint();
			NextUint();
		}

		public uint NextUint()
		{
			_s ^= _s << 13;
			_s ^= _s >> 17;
			_s ^= _s << 5;
			return _s;
		}

		public float NextFloat()
		{
			return (NextUint() & 0xFFFFFF) / 16777216f;
		}

		public float Range(float a, float b)
		{
			return a + (b - a) * NextFloat();
		}

		public bool NextBool()
		{
			return (NextUint() & 1u) != 0u;
		}
	}

	/// <summary>
	///     One river reach: a straight thalweg between two points, with the water
	///     surface at each end and the carve profile widths. Blittable, stored in a
	///     per-continent list. Port of RTF River + RiverConfig + RiverWarp, flattened.
	/// </summary>
	public unsafe struct TerraRiverSeg
	{
		public const int PROFILE = 2; // water level at P1 (0) and P2 (1)

		public float2 P1;
		public float2 P2;
		public float2 Dir;  // normalised P1→P2
		public float2 Norm; // left normal
		public float  InvLen2;
		public float2 Min;  // bbox (padded by valley + meander)
		public float2 Max;

		/// <summary>
		///     Water surface in blocks at each end: index 0 = P1, index 1 = P2. Built
		///     by the coast-up trace so it is monotonic along the river and always sits
		///     at least an incision below the natural ground — the reach is a carved
		///     bed, never a sheet of water standing above the land.
		/// </summary>
		public fixed float Water[PROFILE];

		public float BedWidth;
		public float BankWidth;
		public float ValleyRadius;
		public float BedDepth;
		public float BankHeight;

		public float MeanderAmp;
		public float MeanderFreq;
		public float MeanderPhase;
		public float WarpNoiseFreq;
		public int   WarpSeed;
	}

	/// <summary>
	///     River network generator (RTF Rivermap / BaseRiverGenerator, adapted to
	///     Burst). Per continent (keyed by its corrected voronoi centre) it grows a
	///     set of rivers, each traced UPSTREAM from a coast mouth toward the interior
	///     highlands. Tracing from the coast guarantees every river reaches the sea;
	///     the water surface is built strictly monotonic and always below the natural
	///     ground, so each river is a carved bed that flows downhill from the hills to
	///     the sea instead of a flat lake that pools inland.
	/// </summary>
	[BurstCompile]
	public static class TerraRiverNet
	{
		private const int   MAX_SEGMENTS = 2400; // whole continent's reaches
		private const float MAX_CLIMB    = 60f;  // blocks of bank a valley may incise before it fades

		private const float STEP        = 100f; // trace step, blocks
		private const int   MAX_STEP    = 130;  // reach cap per river (≈13 km)
		private const float INLAND_BIAS = 12f;  // steer the trace toward the interior
		private const float MAX_RISE    = 30f;  // max blocks the water surface climbs per reach
		private const int   STALL_STOP  = 20;   // steps without new high ground before the river ends
		private const float MERGE_DIST  = 30f;  // stop a river when it reaches an existing one (confluence)

		/// <summary>
		///     Build the whole network for one continent, appending reaches to `segs`.
		///     Deterministic in the continent centre + world seed. Mouths are spread by
		///     angle around the coastline so rivers don't bunch together.
		/// </summary>
		public static void BuildForCenter(int2 center, in TerraGenSettings s, in TerraLevels levels,
		                                  ref NativeList<TerraRiverSeg> segs)
		{
			var centerEdge = TerraContinent.GetEdgeValue(center.x, center.y, in s);
			if (centerEdge < s.Inland) return;

			var seed = (uint)(center.x * 73856093) ^ (uint)(center.y * 19349663) ^ (uint)(s.Seed * 83492791);
			var rng  = new TerraRng(seed);

			var count = math.max(1, s.RiverCount);
			for (var i = 0; i < count; i++)
			{
				if (segs.Length >= MAX_SEGMENTS) return;

				// a coastal direction, evenly spread with jitter so mouths don't cluster
				var angle = (i + rng.Range(-0.35f, 0.35f)) * (6.2831855f / count);
				var dx    = math.sin(angle);
				var dz    = math.cos(angle);

				// GetDistanceToOcean stops at shallow ocean (edge≈ShallowOcean); the
				// actual coastline (edge≈Coast) is further inland. Binary-search the ray
				// for where the edge value crosses Coast and put the mouth just inside.
				var toOcean = TerraContinent.GetDistanceToOcean(center.x, center.y, dx, dz, in s);
				if (toOcean < 600f) continue; // continent too small this way

				var lo = 0f;
				var hi = toOcean;
				for (var b = 0; b < 24; b++)
				{
					var mid  = 0.5f * (lo + hi);
					var edge = TerraContinent.GetEdgeValue(center.x + dx * mid, center.y + dz * mid, in s);
					if (edge > s.Coast) lo = mid; else hi = mid; // stay on the land side
				}

				var coastDist = math.max(0f, lo - 20f); // 20 blocks inland of the waterline
				var mouth     = new float2(center.x + dx * coastDist, center.y + dz * coastDist);
				if (TerraContinent.GetEdgeValue(mouth.x, mouth.y, in s) < s.Coast) continue;

				// reaches laid so far — a later river stops when it meets one of these,
				// so channels form a branching tree and never pile up into a flood hub
				var mergeLimit = segs.Length;
				TraceUpstream(mouth, center, mergeLimit, in s, in levels, ref segs, ref rng);
			}
		}

		/// <summary>
		///     Trace one river from its coast mouth up into the interior, then lay its
		///     reaches. Path-finding and water assignment are separate:
		///
		///     1. Path — from the mouth we walk inland toward rising ground (steepest
		///        ascent with an inland bias). Small dips are allowed so the river winds
		///        through rolling terrain instead of stopping at the first bump; it ends
		///        at the highlands, at the continent edge, or after MAX_STEP.
		///     2. Water — for each node the ideal bed water is (ground − incision). We
		///        take the running minimum from the source down to the mouth, so the
		///        surface is monotonic (never rises going downstream) and always below
		///        the ground (a carved bed). That is a river flowing downhill to the sea
		///        with no pooled lake and no standing wall of water.
		/// </summary>
		private static unsafe void TraceUpstream(float2 mouth, int2 center, int mergeLimit,
		                                         in TerraGenSettings s, in TerraLevels levels,
		                                         ref NativeList<TerraRiverSeg> segs, ref TerraRng rng)
		{
			var incision = s.RiverBedDepth + s.NetBankHeight;
			var source   = math.max(80f, s.MountainHeight * 0.45f); // highland stop elevation

			var px = stackalloc float[MAX_STEP + 1];
			var pz = stackalloc float[MAX_STEP + 1];
			var pt = stackalloc float[MAX_STEP + 1];

			var pos  = mouth;
			var terr = TerraHeightmap.SampleLandHeightBlocks(pos.x, pos.y, in s, in levels);
			px[0] = pos.x; pz[0] = pos.y; pt[0] = terr;
			var n = 1;

			var heading = math.atan2(center.x - pos.x, center.y - pos.y);
			if (float.IsNaN(heading)) heading = rng.NextFloat() * 6.2831855f;

			var bestTerrSeen  = terr;
			var sinceImproved = 0;

			for (var step = 0; step < MAX_STEP; step++)
			{
				var ivx = center.x - pos.x;
				var ivz = center.y - pos.y;
				var il  = math.sqrt(ivx * ivx + ivz * ivz);
				if (il > 1e-3f) { ivx /= il; ivz /= il; }

				// fan candidate steps; prefer the one that climbs most, biased inland
				var bestScore = float.MinValue;
				var bestAngle = heading;
				for (var c = -2; c <= 2; c++)
				{
					var a  = heading + c * 0.42f;
					var ax = math.sin(a);
					var az = math.cos(a);
					var p  = new float2(pos.x + ax * STEP, pos.y + az * STEP);
					var th = TerraHeightmap.SampleLandHeightBlocks(p.x, p.y, in s, in levels);
					var score = th + INLAND_BIAS * (ax * ivx + az * ivz);
					if (score > bestScore)
					{
						bestScore = score;
						bestAngle = a;
					}
				}

				heading += AngleDelta(heading, bestAngle) * 0.55f;
				var nx   = math.sin(heading);
				var nz   = math.cos(heading);
				var next = new float2(pos.x + nx * STEP, pos.y + nz * STEP);

				// don't wander back off the continent
				if (TerraContinent.GetEdgeValue(next.x, next.y, in s) < s.Coast) break;

				// stop when we reach an already-traced river — that's a confluence.
				// Prevents independent rivers from piling their reaches into the same
				// low area and flooding it; instead they join into a branching tree.
				if (ReachesExistingRiver(next, segs, mergeLimit))
				{
					px[n] = next.x; pz[n] = next.y;
					pt[n] = TerraHeightmap.SampleLandHeightBlocks(next.x, next.y, in s, in levels);
					n++;
					break;
				}

				var nextTerr = TerraHeightmap.SampleLandHeightBlocks(next.x, next.y, in s, in levels);
				px[n] = next.x; pz[n] = next.y; pt[n] = nextTerr; n++;
				pos = next; terr = nextTerr;

				if (nextTerr >= source) break; // reached the highland source

				// dips are allowed, but a long run without any new high ground means
				// we've flattened into an interior basin — end the river there
				if (nextTerr > bestTerrSeen + 0.5f) { bestTerrSeen = nextTerr; sinceImproved = 0; }
				else sinceImproved++;
				if (sinceImproved > STALL_STOP) break;
			}

			if (n < 2) return;

			// water surface: running minimum of (ground − incision) from source to mouth
			// makes it monotonic downhill and never above ground
			var w  = stackalloc float[MAX_STEP + 1];
			w[n - 1] = pt[n - 1] - incision;
			for (var i = n - 2; i >= 0; i--)
				w[i] = math.min(pt[i] - incision, w[i + 1]);

			// cap how fast the surface climbs per reach so a steep mountain source is a
			// lively stream, not a near-vertical sheet. Still ≤ ground−incision (carved
			// bed preserved); where the cap bites hard the reach fades out via MAX_CLIMB.
			for (var i = 1; i < n; i++)
				w[i] = math.min(w[i], w[i - 1] + MAX_RISE);

			for (var i = 0; i < n - 1 && segs.Length < MAX_SEGMENTS; i++)
				AddReach(ref segs,
				         new float2(px[i], pz[i]), new float2(px[i + 1], pz[i + 1]),
				         w[i], w[i + 1],
				         s.NetBedWidth, s.NetBankWidth, s.NetValleyRadius,
				         s.RiverBedDepth, s.NetBankHeight, ref rng);
		}

		/// <summary> True if `p` is within MERGE_DIST of any reach in [0, limit). </summary>
		private static bool ReachesExistingRiver(float2 p, in NativeList<TerraRiverSeg> segs, int limit)
		{
			for (var i = 0; i < limit; i++)
			{
				TerraRiverSeg seg = segs[i];
				if (p.x < seg.Min.x || p.x > seg.Max.x || p.y < seg.Min.y || p.y > seg.Max.y) continue;
				var t = math.saturate(((p.x - seg.P1.x) * (seg.P2.x - seg.P1.x) +
				                       (p.y - seg.P1.y) * (seg.P2.y - seg.P1.y)) * seg.InvLen2);
				var projX = seg.P1.x + (seg.P2.x - seg.P1.x) * t;
				var projY = seg.P1.y + (seg.P2.y - seg.P1.y) * t;
				var dx    = p.x - projX;
				var dy    = p.y - projY;
				if (dx * dx + dy * dy < MERGE_DIST * MERGE_DIST) return true;
			}

			return false;
		}

		/// <summary> Shortest signed angular difference from → to, in radians. </summary>
		private static float AngleDelta(float from, float to)
		{
			var d = to - from;
			while (d > 3.14159265f) d -= 6.2831855f;
			while (d < -3.14159265f) d += 6.2831855f;
			return d;
		}

		/// <summary> Emit one short reach with a 2-point water profile (P1 then P2). </summary>
		private static unsafe void AddReach(ref NativeList<TerraRiverSeg> segs, float2 p1, float2 p2,
		                                    float water1, float water2,
		                                    int bedWidth, int bankWidth, int valleyRadius,
		                                    int bedDepth, int bankHeight, ref TerraRng rng)
		{
			var d   = p2 - p1;
			var len = math.length(d);
			if (len < 1e-3f) return;
			var dir  = d / len;
			var norm = new float2(dir.y, -dir.x);

			// subtle meander only — the trace already follows the valley
			var meanderAmp  = math.min(valleyRadius * 0.3f, 4f + rng.NextFloat() * 5f);
			var meanderFreq = 0.5f + rng.NextFloat() * 0.5f;
			var pad         = valleyRadius + meanderAmp + 4f;

			var seg = new TerraRiverSeg
			          {
				          P1      = p1,
				          P2      = p2,
				          Dir     = dir,
				          Norm    = norm,
				          InvLen2 = 1f / (len * len),
				          Min     = new float2(math.min(p1.x, p2.x) - pad, math.min(p1.y, p2.y) - pad),
				          Max     = new float2(math.max(p1.x, p2.x) + pad, math.max(p1.y, p2.y) + pad),
				          BedWidth     = bedWidth,
				          BankWidth    = bankWidth,
				          ValleyRadius = valleyRadius,
				          BedDepth     = bedDepth,
				          BankHeight   = bankHeight,
				          MeanderAmp    = meanderAmp,
				          MeanderFreq   = meanderFreq,
				          MeanderPhase  = rng.NextFloat() * 6.2831855f,
				          WarpNoiseFreq = 1f / (90f + rng.NextFloat() * 60f),
				          WarpSeed      = (int)rng.NextUint()
			          };

			seg.Water[0] = water1;
			seg.Water[1] = water2;
			segs.Add(seg);
		}

		/// <summary>
		///     Carve this column against every reach of its continent's network. A reach
		///     cuts a narrow channel: a flat bed of BedWidth flooded to the (monotonic)
		///     water surface, sloping up to the natural ground at the valley rim. Because
		///     the water surface is always below the surrounding ground, the result is a
		///     river running in a bed — no lake, no wall of water.
		/// </summary>
		public static unsafe void CarveColumn(ref TerraCell cell, float x, float z,
		                                      in NativeArray<TerraRiverSeg> segs, int start, int count,
		                                      in TerraGenSettings s, in TerraLevels levels)
		{
			if (count == 0) return;

			var natural    = levels.ToBlocksF(cell.Height);
			var bestTarget = natural;         // deepest carve across all reaches (valley shape)
			var chanBed    = natural;         // deepest bed among channels covering this column
			var chanWater  = float.MinValue;  // that channel's water surface
			var bestMask   = 1f;

			for (var si = start; si < start + count; si++)
			{
				TerraRiverSeg seg = segs[si];
				if (x < seg.Min.x || x > seg.Max.x || z < seg.Min.y || z > seg.Max.y) continue;

				// meander: warp the query point along the reach normal
				var t0 = math.saturate(((x - seg.P1.x) * (seg.P2.x - seg.P1.x) +
				                        (z - seg.P1.y) * (seg.P2.y - seg.P1.y)) * seg.InvLen2);
				var wiggle = math.sin(t0 * seg.MeanderFreq * 6.2831855f + seg.MeanderPhase) * seg.MeanderAmp
				             + TerraNoise.PerlinSigned(x, z, seg.WarpSeed, seg.WarpNoiseFreq, 2)
				             * seg.MeanderAmp * 0.7f;
				var qx = x + seg.Norm.x * wiggle;
				var qz = z + seg.Norm.y * wiggle;

				var t = math.saturate(((qx - seg.P1.x) * (seg.P2.x - seg.P1.x) +
				                       (qz - seg.P1.y) * (seg.P2.y - seg.P1.y)) * seg.InvLen2);
				var projX = seg.P1.x + (seg.P2.x - seg.P1.x) * t;
				var projZ = seg.P1.y + (seg.P2.y - seg.P1.y) * t;
				var dist  = math.sqrt((qx - projX) * (qx - projX) + (qz - projZ) * (qz - projZ));

				if (dist >= seg.ValleyRadius) continue;

				// continuous monotonic water surface along the reach
				var f     = t * (TerraRiverSeg.PROFILE - 1);
				var k     = math.min((int)f, TerraRiverSeg.PROFILE - 2);
				var water = math.lerp(seg.Water[k], seg.Water[k + 1], f - k);

				var bed = water - seg.BedDepth;

				// fade the reach out where it would have to gorge deep below high ground
				var climb = natural - water;
				if (climb > MAX_CLIMB) continue;
				var climbFade = 1f - math.saturate((climb - MAX_CLIMB * 0.5f) / (MAX_CLIMB * 0.5f));
				if (climbFade <= 0.02f) continue;

				// cross-section: flat bed, then slope up to natural ground at the rim
				float target;
				if (dist < seg.BedWidth)
					target = bed;
				else
				{
					var p = Smooth((dist - seg.BedWidth) / math.max(1e-3f, seg.ValleyRadius - seg.BedWidth));
					target = math.lerp(bed, natural, p);
				}

				target = math.lerp(natural, target, climbFade);

				// the whole valley carves the terrain (broad shape)…
				if (target < bestTarget) bestTarget = target;

				// …but WATER only fills the inner channel (within the bank width), so a
				// river running through a natural low basin stays a channel instead of
				// flooding the whole basin into a lake. Bed and water are tied to the
				// same deepest channel here, so a column belongs to one river — no
				// terraced water where two rivers cross.
				if (dist < seg.BankWidth && target < chanBed)
				{
					chanBed   = target;
					chanWater = water;
				}
				if (climbFade > 0.3f) bestMask = math.min(bestMask, dist / seg.ValleyRadius);
			}

			if (bestTarget < natural)
				cell.Height = levels.FromBlocksF(bestTarget);

			// river wherever the (deepest) ground sits below the channel's water surface
			if (chanWater > float.MinValue && bestTarget < chanWater - 0.25f && chanWater >= 0.5f)
			{
				cell.Terrain         = TerraTerrain.River;
				cell.RiverWaterLevel = levels.FromBlocksF(chanWater);
			}

			cell.RiverMask = math.min(cell.RiverMask, bestMask);
		}

		private static float Smooth(float t)
		{
			t = math.saturate(t);
			return t * t * (3f - 2f * t);
		}
	}
}
