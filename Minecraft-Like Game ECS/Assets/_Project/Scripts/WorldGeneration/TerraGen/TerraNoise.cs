using Unity.Burst;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Burst port of ReTerraForged's NoiseUtil + noise modules (Perlin, PerlinRidge,
	///     Billow, WorleyEdge, domain warp, terrace/steps and the functional erosion
	///     module used for "fancy" mountains).
	///     Everything is a pure function of (x, z, seed) → deterministic worldgen without
	///     managed state, so it can run inside IJobParallelFor.
	///     RTF's 256-entry CELL_2D jitter table is replaced by a procedural hash with the
	///     same value range; worlds are not bit-compatible with Java RTF but the
	///     characteristics are identical.
	/// </summary>
	[BurstCompile]
	public static class TerraNoise
	{
		public const int X_PRIME = 1619;
		public const int Y_PRIME = 31337;

		// ── Hashing (exact NoiseUtil ports) ───────────────────────────────────

		public static int Hash2D(int seed, int x, int y)
		{
			var hash = seed;
			hash ^= X_PRIME * x;
			hash ^= Y_PRIME * y;
			hash =  hash * hash * hash * 60493;
			hash ^= hash >> 13;
			return hash;
		}

		/// <summary> Value noise at integer coords, range [-1,1]. </summary>
		public static float ValCoord2D(int seed, int x, int y)
		{
			var n = seed;
			n ^= X_PRIME * x;
			n ^= Y_PRIME * y;
			return n * n * n * 60493 / 2.14748365e9f;
		}

		/// <summary> Cell value mapped to [0,1] (AbstractContinent.getCellValue). </summary>
		public static float CellValue01(int seed, int x, int y)
		{
			return 0.5f + ValCoord2D(seed, x, y) * 0.5f;
		}

		/// <summary>
		///     Voronoi cell jitter vector in [0.05, 0.95]² (replaces NoiseUtil.CELL_2D table).
		/// </summary>
		public static float2 CellVec(int seed, int x, int y)
		{
			var h  = Hash2D(seed, x, y);
			var vx = 0.05f + 0.9f * ((h & 1023) * (1f / 1023f));
			var vy = 0.05f + 0.9f * (((h >> 10) & 1023) * (1f / 1023f));
			return new float2(vx, vy);
		}

		/// <summary> One of NoiseUtil.GRAD_2D's 8 gradients. </summary>
		private static float2 Grad2D(int seed, int x, int y)
		{
			var hash = seed;
			hash ^= X_PRIME * x;
			hash ^= Y_PRIME * y;
			hash =  hash * hash * hash * 60493;
			hash ^= hash >> 13;
			return (hash & 0x7) switch
			       {
				       0 => new float2(-1f, -1f),
				       1 => new float2(1f, -1f),
				       2 => new float2(-1f, 1f),
				       3 => new float2(1f, 1f),
				       4 => new float2(0f, -1f),
				       5 => new float2(-1f, 0f),
				       6 => new float2(0f, 1f),
				       _ => new float2(1f, 0f)
			       };
		}

		private static float GradCoord2D(int seed, int x, int y, float xd, float yd)
		{
			float2 g = Grad2D(seed, x, y);
			return xd * g.x + yd * g.y;
		}

		// ── Small math helpers (NoiseUtil ports) ──────────────────────────────

		public static int Floor(float f)
		{
			return f >= 0f ? (int)f : (int)f - 1;
		}

		public static int Round(float f)
		{
			return f >= 0f ? (int)(f + 0.5f) : (int)(f - 0.5f);
		}

		/// <summary> map(value, min, max, range): clamp then normalise to [0,1]. </summary>
		public static float Map(float value, float min, float max, float range)
		{
			var dif = math.clamp(value, min, max) - min;
			return dif >= range ? 1f : dif / range;
		}

		/// <summary> Remap [min,max] → [from,to]. </summary>
		public static float MapRange(float value, float from, float to, float min, float max)
		{
			var alpha = (value - min) / (max - min);
			return from + alpha * (to - from);
		}

		public static float InterpHermite(float t)
		{
			return t * t * (3f - 2f * t);
		}

		public static float InterpQuintic(float t)
		{
			return t * t * t * (t * (t * 6f - 15f) + 10f);
		}

		public static float CopySign(float value, float sign)
		{
			if (sign < 0f && value > 0f || sign > 0f && value < 0f) return -value;
			return value;
		}

		public enum Interp : byte
		{
			Linear = 0,
			Curve3 = 1, // hermite
			Curve4 = 2  // quintic
		}

		public static float ApplyInterp(float t, Interp interp)
		{
			return interp switch
			       {
				       Interp.Curve3 => InterpHermite(t),
				       Interp.Curve4 => InterpQuintic(t),
				       _             => t
			       };
		}

		// ── Perlin (RTF gradient noise, exact port) ───────────────────────────

		/// <summary> Single octave, range roughly [-1,1]. </summary>
		public static float PerlinSample(float x, float y, int seed, Interp interp)
		{
			var   x0 = Floor(x);
			var   y0 = Floor(y);
			var   x1 = x0 + 1;
			var   y1 = y0 + 1;
			var   xs = ApplyInterp(x - x0, interp);
			var   ys = ApplyInterp(y - y0, interp);
			var   xd0 = x - x0;
			var   yd0 = y - y0;
			var   xd1 = xd0 - 1f;
			var   yd1 = yd0 - 1f;
			var   xf0 = math.lerp(GradCoord2D(seed, x0, y0, xd0, yd0), GradCoord2D(seed, x1, y0, xd1, yd0), xs);
			var   xf1 = math.lerp(GradCoord2D(seed, x0, y1, xd0, yd1), GradCoord2D(seed, x1, y1, xd1, yd1), xs);
			return math.lerp(xf0, xf1, ys);
		}

		/// <summary> RTF Perlin octave amplitude normalisation table. </summary>
		private static float Signal(int octaves)
		{
			return math.min(octaves, 6) switch
			       {
				       0 => 1f,
				       1 => 0.9f,
				       2 => 0.83f,
				       3 => 0.75f,
				       4 => 0.64f,
				       5 => 0.62f,
				       _ => 0.61f
			       };
		}

		/// <summary> Fractal perlin normalised to [0,1] (RTF Perlin module). </summary>
		public static float Perlin(float x, float z, int seed, float frequency, int octaves,
		                           float lacunarity = 2f, float gain = 0.5f, Interp interp = Interp.Curve3)
		{
			x *= frequency;
			z *= frequency;

			var sum = 0f;
			var amp = gain;
			for (var i = 0; i < octaves; i++)
			{
				sum += PerlinSample(x, z, seed + i, interp) * amp;
				x   *= lacunarity;
				z   *= lacunarity;
				amp *= gain;
			}

			// bounds identical to RTF Perlin.min/max
			var signal = Signal(octaves);
			var max    = 0f;
			amp = gain;
			for (var i = 0; i < octaves; i++)
			{
				max += signal * amp;
				amp *= gain;
			}

			return Map(sum, -max, max, max * 2f);
		}

		/// <summary> Signed fractal perlin, roughly [-1,1] (no normalisation to [0,1]). </summary>
		public static float PerlinSigned(float x, float z, int seed, float frequency, int octaves,
		                                 float lacunarity = 2f, float gain = 0.5f)
		{
			x *= frequency;
			z *= frequency;
			var sum = 0f;
			var amp = 1f;
			var tot = 0f;
			for (var i = 0; i < octaves; i++)
			{
				sum += PerlinSample(x, z, seed + i, Interp.Curve3) * amp;
				tot += amp;
				x   *= lacunarity;
				z   *= lacunarity;
				amp *= gain;
			}

			return sum / tot;
		}

		/// <summary> Ridged multifractal, [0,1] (RTF PerlinRidge module). </summary>
		public static float Ridge(float x, float z, int seed, float frequency, int octaves,
		                          float lacunarity = 2f, float gain = 0.5f, Interp interp = Interp.Curve3)
		{
			x *= frequency;
			z *= frequency;
			var amp    = 2f;
			var value  = 0f;
			var weight = 1f;
			var maxVal = 0f;
			var maxAmp = 2f;
			var maxWeight = 1f;
			var spectral = 1f; // lacunarity^-i
			for (var i = 0; i < octaves; i++)
			{
				var signal = PerlinSample(x, z, seed + i, interp);
				signal =  1f - math.abs(signal);
				signal *= signal;
				signal *= weight;
				weight =  math.clamp(signal * amp, 0f, 1f);
				value  += signal * spectral;

				// max bound accumulation (RTF calculateMaxBound)
				var maxSignal = 1f * maxWeight;
				maxWeight =  math.clamp(maxSignal * maxAmp, 0f, 1f);
				maxVal    += maxSignal * spectral;
				maxAmp    *= gain;

				x        *= lacunarity;
				z        *= lacunarity;
				amp      *= gain;
				spectral /= lacunarity;
			}

			return Map(value, 0f, maxVal, maxVal);
		}

		/// <summary> Billow noise, [0,1] (RTF Billow = 1 - ridge accumulation). </summary>
		public static float Billow(float x, float z, int seed, float frequency, int octaves,
		                           float lacunarity = 2f, float gain = 0.5f)
		{
			return 1f - Ridge(x, z, seed, frequency, octaves, lacunarity, gain);
		}

		// ── Worley / cellular ────────────────────────────────────────────────

		public enum EdgeFunc : byte
		{
			Distance2    = 0, // d2 - 1
			Distance2Add = 1, // d2 + d1 - 1
			Distance2Sub = 2, // d2 - d1 - 1
			Distance2Div = 3  // d1 / d2 - 1
		}

		public enum DistFunc : byte
		{
			Euclidean = 0,
			Natural   = 1 // euclidean² + manhattan blend (FastNoise "Natural")
		}

		public static float DistApply(DistFunc f, float dx, float dy)
		{
			return f switch
			       {
				       DistFunc.Natural => math.abs(dx) + math.abs(dy) + (dx * dx + dy * dy),
				       _                => dx * dx + dy * dy
			       };
		}

		/// <summary>
		///     Worley edge value in [0,1] — 1 near cell borders for Distance2* functions.
		///     Port of RTF's WorleyEdge noise module (jitter fixed at RTF's 0.7 for
		///     region-style lookups, distances sqrt'd for euclidean).
		/// </summary>
		public static float WorleyEdge(float x, float z, int seed, float frequency,
		                               EdgeFunc edge, DistFunc distFunc, float jitter = 1f)
		{
			x *= frequency;
			z *= frequency;
			var xi = Floor(x);
			var zi = Floor(z);
			var d1 = float.MaxValue;
			var d2 = float.MaxValue;
			for (var dz = -1; dz <= 1; dz++)
			for (var dx = -1; dx <= 1; dx++)
			{
				var    cx  = xi + dx;
				var    cz  = zi + dz;
				float2 vec = CellVec(seed, cx, cz);
				var    px  = cx + vec.x * jitter;
				var    pz  = cz + vec.y * jitter;
				var    d   = DistApply(distFunc, px - x, pz - z);
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

			if (distFunc == DistFunc.Euclidean)
			{
				d1 = math.sqrt(d1);
				d2 = math.sqrt(d2);
			}

			float value, min, max;
			switch (edge)
			{
				case EdgeFunc.Distance2Add:
					value = d2 + d1 - 1f;
					min   = -1f;
					max   = 1.6f;
					break;
				case EdgeFunc.Distance2Sub:
					value = d2 - d1 - 1f;
					min   = -1f;
					max   = 0.8f;
					break;
				case EdgeFunc.Distance2Div:
					value = d1 / d2 - 1f;
					min   = -1f;
					max   = 0f;
					break;
				default: // Distance2
					value = d2 - 1f;
					min   = -1f;
					max   = 1f;
					break;
			}

			return Map(value, min, max, max - min);
		}

		// ── Domain warp ──────────────────────────────────────────────────────

		/// <summary>
		///     RTF Noises.warpPerlin — offsets the coordinate by two independent signed
		///     perlin fields scaled by strength.
		/// </summary>
		public static void WarpPerlin(ref float x, ref float z, int seed, float scale, int octaves, float strength)
		{
			var freq = 1f / math.max(1f, scale);
			var ox   = PerlinSigned(x, z, seed, freq, octaves);
			var oz   = PerlinSigned(x, z, seed + 31, freq, octaves);
			x += ox * strength;
			z += oz * strength;
		}

		// ── Value modifiers (RTF module ports/approximations) ─────────────────

		/// <summary> RTF Alpha module: v*alpha + (1-alpha). </summary>
		public static float Alpha(float value, float alpha)
		{
			return value * alpha + (1f - alpha);
		}

		/// <summary> RTF Boost approximation — pushes mid/high values up. </summary>
		public static float Boost(float value)
		{
			value = math.saturate(value);
			return 1f - (1f - value) * (1f - value);
		}

		/// <summary> RTF PowCurve approximation. </summary>
		public static float PowCurve(float value, float power)
		{
			return math.pow(math.saturate(value), power);
		}

		/// <summary> RTF Blend module: lerp between a and b using selector remapped by [min,max]. </summary>
		public static float Blend(float selector, float a, float b, float min, float max)
		{
			var alpha = math.saturate((selector - min) / math.max(1e-6f, max - min));
			return math.lerp(a, b, alpha);
		}

		/// <summary> RTF Threshold module. </summary>
		public static float Threshold(float selector, float below, float above, float threshold)
		{
			return selector < threshold ? below : above;
		}

		/// <summary>
		///     Smooth terrace / steps (approximation of RTF Steps + Terrace modules).
		///     Quantises value into `steps` levels, blending between plateaus over
		///     [slopeMin, slopeMax] of each step's span.
		/// </summary>
		public static float Steps(float value, int steps, float slopeMin, float slopeMax,
		                          Interp interp = Interp.Linear)
		{
			value = math.saturate(value);
			var f     = value * steps;
			var level = math.floor(f);
			var frac  = f - level;
			var range = math.max(1e-6f, slopeMax - slopeMin);
			var alpha = math.saturate((frac - slopeMin) / range);
			alpha = ApplyInterp(alpha, interp);
			return math.saturate((level + alpha) / steps);
		}

		/// <summary>
		///     Approximation of RTF AdvancedTerrace — terraces modulated and masked by
		///     noise so steps read as natural strata rather than perfect stairs.
		/// </summary>
		public static float AdvancedTerrace(float value, float modulation, float mask, float slope,
		                                    float blendMin, float blendMax, int steps)
		{
			var stepped = Steps(value, steps, 0f, math.saturate(slope), Interp.Curve3);
			stepped = math.saturate(stepped + modulation * (1f / math.max(1, steps)) * 0.5f);
			var blend = Map(value, blendMin, blendMax, math.max(1e-6f, blendMax - blendMin));
			return math.lerp(value, stepped, blend * mask);
		}

		// ── Functional erosion (port of RTF noise/module/Erosion.java) ────────

		/// <summary>
		///     Cheap, seam-free erosion approximation: measures distance from ridge
		///     lines formed by connecting each voronoi point to its lowest neighbour,
		///     then blends the input height toward that erosion channel value.
		///     Used for RTF "fancy mountains".
		/// </summary>
		public static unsafe float ErodedNoise(float x, float z, int seed, int octaves, float strength,
		                                       float gridSize, float amplitude, float lacunarity,
		                                       float distanceFallOff, float inputValue,
		                                       float inputFreq, int inputSeed, int inputOctaves,
		                                       float inputLacunarity, float inputGain)
		{
			var sum      = 0f;
			var max      = 0f;
			var gain     = 1f;
			var distance = gridSize;
			var px       = x;
			var pz       = z;
			var cache    = stackalloc float[25];
			for (var i = 0; i < octaves; i++)
			{
				var value = SingleErosionValue(px, pz, seed, distance, cache,
				                               inputFreq, inputSeed, inputOctaves, inputLacunarity, inputGain);
				sum      += value * gain;
				max      += gain;
				gain     *= amplitude;
				distance *= distanceFallOff;
				px       *= lacunarity;
				pz       *= lacunarity;
			}

			var erosion = sum / max;
			// BlendMode.CONSTANT: lerp(erosion, value, 1 - strength)
			return math.lerp(erosion, inputValue, 1f - strength);
		}

		private static unsafe float SingleErosionValue(float x, float y, int seed, float gridSize, float* cache,
		                                               float inputFreq, int inputSeed, int inputOctaves,
		                                               float inputLacunarity, float inputGain)
		{
			for (var i = 0; i < 25; i++) cache[i] = -1f;

			var pix        = Floor(x / gridSize);
			var piy        = Floor(y / gridSize);
			var minHeight2 = float.MaxValue;
			for (var dy1 = -1; dy1 <= 1; dy1++)
			for (var dx1 = -1; dx1 <= 1; dx1++)
			{
				var    pax  = pix + dx1;
				var    pay  = piy + dy1;
				float2 vec1 = CellVec(seed, pax, pay);
				var    ax   = (pax + vec1.x) * gridSize;
				var    ay   = (pay + vec1.y) * gridSize;
				var    bx   = ax;
				var    by   = ay;

				var lowestNeighbour = float.MaxValue;
				for (var dy2 = -1; dy2 <= 1; dy2++)
				for (var dx2 = -1; dx2 <= 1; dx2++)
				{
					var    pbx  = pax + dx2;
					var    pby  = pay + dy2;
					float2 vec2 = pbx == pax && pby == pay ? vec1 : CellVec(seed, pbx, pby);
					var    cx   = (pbx + vec2.x) * gridSize;
					var    cy   = (pby + vec2.y) * gridSize;

					// cache keyed by combined offset from centre cell (RTF getNoiseValue)
					var cacheIndex = (dy1 + dy2 + 2) * 5 + (dx1 + dx2 + 2);
					var height     = cache[cacheIndex];
					if (height < 0f)
					{
						height = Perlin(cx, cy, inputSeed, inputFreq, inputOctaves,
						                inputLacunarity, inputGain);
						cache[cacheIndex] = height;
					}

					if (!(height < lowestNeighbour)) continue;
					lowestNeighbour = height;
					bx              = cx;
					by              = cy;
				}

				var height2 = SegmentDist2(x, y, ax, ay, bx, by);
				if (height2 < minHeight2) minHeight2 = height2;
			}

			return math.clamp(math.sqrt(minHeight2) / gridSize, 0f, 1f);
		}

		private static float SegmentDist2(float px, float py, float ax, float ay, float bx, float by)
		{
			var padx = px - ax;
			var pady = py - ay;
			var badx = bx - ax;
			var bady = by - ay;
			var paba = padx * badx + pady * bady;
			var baba = badx * badx + bady * bady;
			var h    = paba == 0f && baba == 0f ? 0f : math.clamp(paba / baba, 0f, 1f);
			var dx   = badx * h - padx;
			var dy   = bady * h - pady;
			return dx * dx + dy * dy;
		}
	}
}
