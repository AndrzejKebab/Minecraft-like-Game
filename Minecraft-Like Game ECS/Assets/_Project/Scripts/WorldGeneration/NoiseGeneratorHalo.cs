using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	/// <summary>
	/// Halo-aware terrain generation.  Fills a (chunkSize*3)² heightmap covering
	/// the 3×3 chunk region centred on a chunk.  Used by deterministic decoration
	/// to find ground level for foreign trees rooted in neighbouring chunks
	/// without reading neighbour BlockData.
	///
	/// One batched GenUniformGrid2D call per noise type is dramatically cheaper
	/// than 9 separate 32×32 calls (FastNoise2 SIMD batches over the whole grid).
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		FloatPrecision = FloatPrecision.Low)]
	public static class NoiseGeneratorHalo
	{
		[BurstCompile]
		public static void GenerateHaloHeightmap(
			out NativeArray<int> haloHeights,
			ref FastNoise        continentalnessNoise,
			ref FastNoise        peaksAndValleysNoise,
			ref FastNoise        erosionNoise,
			ref FastNoise        riverNoise,
			ref int3             chunkWorldPos,
			int                  chunkSize,
			int                  seed,
			ref NativeCurve      continentalnessHeight,
			ref NativeCurve      erosionCurve,
			ref NativeCurve      peaksAndValleysCurve)
		{
			int haloSize = chunkSize * 3;
			int originX  = chunkWorldPos.x - chunkSize;
			int originZ  = chunkWorldPos.z - chunkSize;

			haloHeights = new NativeArray<int>(haloSize * haloSize, Allocator.Temp,
			                                   NativeArrayOptions.UninitializedMemory);

			var contMap = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);
			var pvMap   = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);
			var erosMap = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);
			var rivMap  = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);

			continentalnessNoise.GenUniformGrid2D(contMap, out _, originX, originZ, haloSize, haloSize, 1, 1, seed);
			peaksAndValleysNoise.GenUniformGrid2D(pvMap,   out _, originX, originZ, haloSize, haloSize, 1, 1, seed);
			erosionNoise.GenUniformGrid2D(erosMap,         out _, originX, originZ, haloSize, haloSize, 1, 1, seed);
			riverNoise.GenUniformGrid2D(rivMap,            out _, originX, originZ, haloSize, haloSize, 1, 1, seed);

			for (int z = 0; z < haloSize; z++)
			for (int x = 0; x < haloSize; x++)
			{
				haloHeights[x + z * haloSize] = ComputeHeight(
					contMap[x, z], pvMap[x, z], erosMap[x, z], rivMap[x, z],
					ref continentalnessHeight, ref erosionCurve, ref peaksAndValleysCurve);
			}

			contMap.Dispose();
			pvMap.Dispose();
			erosMap.Dispose();
			rivMap.Dispose();
		}

		// Mirror of NoiseGenerator.ComputeHeight — kept private there.
		// If you want one source of truth, make NoiseGenerator.ComputeHeight public/internal
		// and delete this copy.
		[BurstCompile]
		private static int ComputeHeight(
			float cont, float pv, float eros, float river,
			ref NativeCurve contSpline, ref NativeCurve erosSpline, ref NativeCurve pvSpline)
		{
			const float RIVER_HALF_WIDTH = 0.12f;
			const float MAX_RIVER_CARVE  = 28f;

			float baseHeight    = contSpline.Evaluate(cont);
			float erosionFactor = erosSpline.Evaluate(eros);
			float peaksBonus    = pvSpline.Evaluate(pv);
			float h             = baseHeight + erosionFactor * peaksBonus;

			float riverAbs = math.abs(river);
			if (!(riverAbs < RIVER_HALF_WIDTH) || !(baseHeight > -20f))
				return (int)math.round(h);

			float t     = 1f - riverAbs / RIVER_HALF_WIDTH;
			float carve = t * t * MAX_RIVER_CARVE;
			carve *= math.saturate(1f - erosionFactor * 0.6f);
			h     -= carve;
			return (int)math.round(h);
		}
	}
}