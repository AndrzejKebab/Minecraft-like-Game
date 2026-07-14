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
	///     One river reach: a straight thalweg from an upstream point (t=0, higher
	///     water) to a downstream point (t=1, lower water), plus a meander warp and
	///     the carve profile widths. Blittable, stored in a per-continent list.
	///     Port of RTF River + RiverConfig + RiverWarp, flattened.
	/// </summary>
	public unsafe struct TerraRiverSeg
	{
		public const int PROFILE = 2; // upstream (0) and downstream (1) water levels

		public float2 P1;   // upstream
		public float2 P2;   // downstream
		public float2 Dir;  // normalised P1→P2
		public float2 Norm; // left normal
		public float  InvLen2;
		public float2 Min;  // bbox (padded by valley + meander)
		public float2 Max;

		/// <summary>
		///     Water surface in blocks: index 0 = upstream end (P1), index 1 =
		///     downstream end (P2). Both come from the downhill walk, monotonically
		///     non-increasing downstream, so the reach's water stays at or below the
		///     ground and flows toward the sea.
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
	///     Port of RTF's river network generator (Rivermap / BaseRiverGenerator /
	///     UpliftRiverCarver), adapted to Burst. Per continent (keyed by its corrected
	///     voronoi centre) it grows a branching network: main rivers run from inland
	///     to the coast, tributaries fork off recursively. Rivers are actual connected
	///     line reaches with a downhill water surface, so channels join and flow to
	///     the sea instead of appearing per-column.
	/// </summary>
	[BurstCompile]
	public static class TerraRiverNet
	{
		private const int   MAX_SEGMENTS = 3200; // whole continent's reaches (mains + tributaries)
		private const float MAX_CLIMB    = 70f;  // blocks a river valley may incise below high ground
		                                         // before the reach fades out (won't gorge ridges)
		private const float MAX_FLOOD    = 10f;  // max blocks the water may sit above a column's own
		                                         // ground — stops tall water walls on steep drops

		private const float STEP     = 100f; // walk step, blocks
		private const int   MAX_STEP = 160;  // reach cap per river (≈16 km max)

		/// <summary>
		///     Build the whole network for one continent, appending reaches to `segs`.
		///     Deterministic in the continent centre + world seed.
		///     Each river is grown by a downhill walk from an upland source to the sea
		///     (steepest descent + coastward bias), so channels follow real valleys,
		///     join naturally and flow to the coast — instead of straight radial lines
		///     that gouge canyons through hills. This is the routing RTF gets from its
		///     uplift / water-table field; we approximate it with the greedy walk.
		/// </summary>
		public static void BuildForCenter(int2 center, in TerraGenSettings s, in TerraLevels levels,
		                                  ref NativeList<TerraRiverSeg> segs)
		{
			var centerEdge = TerraContinent.GetEdgeValue(center.x, center.y, in s);
			if (centerEdge < s.Inland) return;

			var seed = (uint)(center.x * 73856093) ^ (uint)(center.y * 19349663) ^ (uint)(s.Seed * 83492791);
			var rng  = new TerraRng(seed);

			for (var i = 0; i < s.RiverCount; i++)
			{
				if (segs.Length >= MAX_SEGMENTS) return;

				// pick a source: a random inland direction, part-way to the coast
				var angle   = rng.NextFloat() * 6.2831855f;
				var dx      = math.sin(angle);
				var dz      = math.cos(angle);
				var toOcean = TerraContinent.GetDistanceToOcean(center.x, center.y, dx, dz, in s);
				if (toOcean < 900f) continue;

				var srcDist = (0.25f + rng.NextFloat() * 0.55f) * toOcean;
				var src     = new float2(center.x + dx * srcDist, center.y + dz * srcDist);
				if (TerraContinent.GetEdgeValue(src.x, src.y, in s) < s.Inland) continue;

				var mainStart = segs.Length;
				WalkRiver(src, center, s.NetBedWidth, s.NetBankWidth, s.NetValleyRadius, MAX_STEP,
				          in s, in levels, ref segs, ref rng);

				// tributaries branch off the trunk (RTF generateForks), recursively —
				// this is what makes the network dense enough to actually encounter
				SpawnTributaries(mainStart, segs.Length - mainStart, center, 0.62f, 0,
				                 in s, in levels, ref segs, ref rng);
			}
		}

		/// <summary>
		///     Spawn tributaries branching off a set of reaches [start, start+count).
		///     Each starts at a point offset to the side of a parent reach and walks
		///     downhill (toward the parent's valley / the coast), recursively spawning
		///     its own tributaries. Depth-capped like RTF's generateForks.
		/// </summary>
		private static void SpawnTributaries(int start, int count, int2 center, float widthScale, int depth,
		                                     in TerraGenSettings s, in TerraLevels levels,
		                                     ref NativeList<TerraRiverSeg> segs, ref TerraRng rng)
		{
			if (depth > 2 || count < 3 || segs.Length >= MAX_SEGMENTS) return;

			var tribs   = depth == 0 ? 3 : 2;
			var maxStep = depth == 0 ? 90 : 55;

			for (var t = 0; t < tribs; t++)
			{
				if (segs.Length >= MAX_SEGMENTS) return;

				// pick a reach along the parent trunk, offset perpendicular to a side
				var ri = start + 1 + (int)(rng.NextFloat() * (count - 2));
				ri = math.clamp(ri, start, start + count - 1);
				TerraRiverSeg pr = segs[ri];

				var side   = rng.NextBool() ? 1f : -1f;
				var offset = 250f + rng.NextFloat() * 500f;
				var src    = new float2(pr.P1.x + pr.Norm.x * side * offset,
				                        pr.P1.y + pr.Norm.y * side * offset);
				if (TerraContinent.GetEdgeValue(src.x, src.y, in s) < s.Inland) continue;

				var tStart = segs.Length;
				WalkRiver(src, center,
				          math.max(2, (int)(s.NetBedWidth * widthScale)),
				          math.max(5, (int)(s.NetBankWidth * widthScale)),
				          math.max(18, (int)(s.NetValleyRadius * widthScale)), maxStep,
				          in s, in levels, ref segs, ref rng);

				SpawnTributaries(tStart, segs.Length - tStart, center, widthScale * 0.7f, depth + 1,
				                 in s, in levels, ref segs, ref rng);
			}
		}

		/// <summary>
		///     Greedy downhill walk from a source to the sea, emitting short reaches.
		///     Water tracks the terrain (minus an incision) and is clamped monotonically
		///     downhill, so valleys stay shallow and the river never flows uphill.
		/// </summary>
		private static void WalkRiver(float2 pos, int2 center,
		                              int bedWidth, int bankWidth, int valleyRadius, int maxStep,
		                              in TerraGenSettings s, in TerraLevels levels,
		                              ref NativeList<TerraRiverSeg> segs, ref TerraRng rng)
		{
			var incision  = s.RiverBedDepth + s.NetBankHeight;
			var terr      = TerraHeightmap.SampleLandHeightBlocks(pos.x, pos.y, in s, in levels);
			var waterCeil = math.max(0f, terr - incision);

			// initial heading: away from the continent centre (coastward)
			var heading = math.atan2(pos.x - center.x, pos.y - center.y);
			if (float.IsNaN(heading)) heading = rng.NextFloat() * 6.2831855f;

			var stall = 0;
			for (var step = 0; step < maxStep && segs.Length < MAX_SEGMENTS; step++)
			{
				// coastward reference direction (unit)
				var cvx = pos.x - center.x;
				var cvz = pos.y - center.y;
				var clen = math.sqrt(cvx * cvx + cvz * cvz);
				if (clen > 1e-3f) { cvx /= clen; cvz /= clen; }

				// sample candidate steps fanned around the heading; pick lowest terrain
				// with a mild coastward bias (keeps flat stretches moving to the sea)
				var bestScore = float.MaxValue;
				var bestAngle = heading;
				var bestTerr  = terr;
				var bestPos   = pos;
				for (var c = -2; c <= 2; c++)
				{
					var a  = heading + c * 0.42f;
					var ax = math.sin(a);
					var az = math.cos(a);
					var p  = new float2(pos.x + ax * STEP, pos.y + az * STEP);
					var th = TerraHeightmap.SampleLandHeightBlocks(p.x, p.y, in s, in levels);
					// prefer downhill, but bias hard toward the coast so the river
					// pushes through the many small local pits of the pre-erosion
					// terrain instead of dead-ending in a basin
					var score = th - 26f * (ax * cvx + az * cvz);
					if (score < bestScore)
					{
						bestScore = score;
						bestAngle = a;
						bestTerr  = th;
						bestPos   = p;
					}
				}

				// momentum: ease the heading toward the chosen candidate
				heading += AngleDelta(heading, bestAngle) * 0.55f;
				var nx   = math.sin(heading);
				var nz   = math.cos(heading);
				var next = new float2(pos.x + nx * STEP, pos.y + nz * STEP);
				var nextTerr = TerraHeightmap.SampleLandHeightBlocks(next.x, next.y, in s, in levels);

				var upWater   = waterCeil;
				var downWater = math.min(waterCeil, math.max(0f, nextTerr - incision));

				AddReach(ref segs, pos, next, upWater, downWater,
				         bedWidth, bankWidth, valleyRadius, s.RiverBedDepth, s.NetBankHeight, ref rng);

				var prevTerr = terr;
				waterCeil = downWater;
				pos       = next;
				terr      = nextTerr;

				if (nextTerr <= 1f) return; // reached the sea

				// stall detection: only give up if we climb persistently (a true
				// mountain wall the coastward bias can't overcome)
				if (nextTerr > prevTerr + 6f) stall++;
				else stall = 0;
				if (stall > 22) return;
			}
		}

		/// <summary> Shortest signed angular difference from → to, in radians. </summary>
		private static float AngleDelta(float from, float to)
		{
			var d = to - from;
			while (d > 3.14159265f) d -= 6.2831855f;
			while (d < -3.14159265f) d += 6.2831855f;
			return d;
		}

		/// <summary> Emit one short reach with a 2-point (up/down) water profile. </summary>
		private static unsafe void AddReach(ref NativeList<TerraRiverSeg> segs, float2 p1, float2 p2,
		                                    float upWater, float downWater,
		                                    int bedWidth, int bankWidth, int valleyRadius,
		                                    int bedDepth, int bankHeight, ref TerraRng rng)
		{
			var d   = p2 - p1;
			var len = math.length(d);
			if (len < 1e-3f) return;
			var dir  = d / len;
			var norm = new float2(dir.y, -dir.x);

			// subtle meander only — the walk already follows the valley
			var meanderAmp   = math.min(valleyRadius * 0.25f, 6f + rng.NextFloat() * 6f);
			var meanderFreq  = 0.5f + rng.NextFloat() * 0.5f;
			var pad          = valleyRadius + meanderAmp + 4f;

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

			seg.Water[0] = upWater;
			seg.Water[1] = downWater;
			segs.Add(seg);
		}


		/// <summary>
		///     Carve every reach of this column's continent network into the cell.
		///     Operates in block space (via the hypsometric curve) then writes the
		///     normalised height back. Sets River terrain + water level in the channel.
		/// </summary>
		public static unsafe void CarveColumn(ref TerraCell cell, float x, float z,
		                                      in NativeArray<TerraRiverSeg> segs, int start, int count,
		                                      in TerraGenSettings s, in TerraLevels levels)
		{
			if (count == 0) return;

			var natural     = levels.ToBlocksF(cell.Height);
			var bestTarget  = natural;         // deepest carve across all reaches (height)
			var nearestDist = float.MaxValue;  // closest channel to this column
			var nearestWater = 0f;             // that channel's water level (fills the V)
			var bestMask    = 1f;

			for (var si = start; si < start + count; si++)
			{
				TerraRiverSeg seg = segs[si];
				if (x < seg.Min.x || x > seg.Max.x || z < seg.Min.y || z > seg.Max.y) continue;

				// meander: warp the query point along the reach normal, phased by
				// the raw projection parameter along the reach
				var t0 = math.saturate(((x - seg.P1.x) * (seg.P2.x - seg.P1.x) +
				                        (z - seg.P1.y) * (seg.P2.y - seg.P1.y)) * seg.InvLen2);

				var wiggle = math.sin(t0 * seg.MeanderFreq * 6.2831855f + seg.MeanderPhase) * seg.MeanderAmp
				             + TerraNoise.PerlinSigned(x, z, seg.WarpSeed, seg.WarpNoiseFreq, 2)
				             * seg.MeanderAmp * 0.7f;
				var qx = x + seg.Norm.x * wiggle;
				var qz = z + seg.Norm.y * wiggle;

				// distance from warped point to the reach
				var t = math.saturate(((qx - seg.P1.x) * (seg.P2.x - seg.P1.x) +
				                       (qz - seg.P1.y) * (seg.P2.y - seg.P1.y)) * seg.InvLen2);
				var projX = seg.P1.x + (seg.P2.x - seg.P1.x) * t;
				var projZ = seg.P1.y + (seg.P2.y - seg.P1.y) * t;
				var dist  = math.sqrt((qx - projX) * (qx - projX) + (qz - projZ) * (qz - projZ));

				if (dist >= seg.ValleyRadius) continue;

				// continuous downhill water surface (monotonic along the reach)
				var f     = t * (TerraRiverSeg.PROFILE - 1);
				var k     = math.min((int)f, TerraRiverSeg.PROFILE - 2);
				var water = math.lerp(seg.Water[k], seg.Water[k + 1], f - k);

				var bed = water - seg.BedDepth;

				// overburden fade: don't plow a sea-level gorge through a ridge —
				// where the natural terrain rises far above the water, the reach
				// isn't there (fades out, resumes past the ridge)
				var climb = natural - water;
				if (climb > MAX_CLIMB) continue;
				var climbFade = 1f - math.saturate((climb - MAX_CLIMB * 0.5f) / (MAX_CLIMB * 0.5f));
				if (climbFade <= 0.02f) continue;

				// steep-drop guard: if this column's own ground is well BELOW the
				// reach's water line, the reach is upslope of a cliff here — flooding
				// it would raise a tall wall of water. Skip; the water follows the
				// terrain down as the reach continues, not as a vertical sheet.
				if (natural < water - MAX_FLOOD) continue;

				if (climbFade > 0.3f)
					bestMask = math.min(bestMask, dist / seg.ValleyRadius);

				// cross-section: flat bed in the middle, sloping up to the natural
				// terrain at the valley rim. Everything the slope leaves below the
				// water line becomes river — so the whole V floods, not just a
				// central ditch.
				float target;
				if (dist < seg.BedWidth)
					target = bed;
				else
				{
					var p = Smooth((dist - seg.BedWidth) / math.max(1e-3f, seg.ValleyRadius - seg.BedWidth));
					target = math.lerp(bed, natural, p);
				}

				target = math.lerp(natural, target, climbFade);

				// deepest carve sets the ground height
				if (target < bestTarget) bestTarget = target;

				// the NEAREST channel sets the water level that floods this column —
				// not the deepest reach (whose water may be far lower, e.g. a
				// downstream reach or a crossing river), which would leave the V dry
				if (dist < nearestDist)
				{
					nearestDist  = dist;
					nearestWater = water;
				}
			}

			if (bestTarget < natural)
				cell.Height = levels.FromBlocksF(bestTarget);

			// river wherever the carved ground sits below the nearest channel's water
			if (nearestWater >= 1f && bestTarget < nearestWater - 0.25f)
			{
				cell.Terrain         = TerraTerrain.River;
				cell.RiverWaterLevel = levels.FromBlocksF(nearestWater);
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
