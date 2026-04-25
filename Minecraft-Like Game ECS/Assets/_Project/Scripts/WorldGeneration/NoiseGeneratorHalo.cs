using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     Halo-aware terrain generation.  Fills a (chunkSize*3)² heightmap covering
	///     the 3×3 chunk region centred on a chunk.  Used by deterministic decoration
	///     to find ground level for foreign trees rooted in neighboring chunks
	///     without reading neighbor BlockData.
	///     One batched GenUniformGrid2D call per noise type is dramatically cheaper
	///     than 9 separate 32×32 calls (FastNoise2 SIMD batches over the whole grid).
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class NoiseGenerator
	{
		// RiverHalfWidth: fraction of abs-noise range [0,1] treated as inside channel.
		//   0.08 = narrow stream   0.13 = normal river   0.20 = broad valley
		private const float RIVER_HALF_WIDTH = 0.25f;
		// MaxRiverCarve: blocks carved at centerline (depth=0 at banks).
		private const float MAX_RIVER_CARVE = 28f;
		
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
			var haloSize = chunkSize * 3;
			var originX  = chunkWorldPos.x - chunkSize;
			var originZ  = chunkWorldPos.z - chunkSize;

			haloHeights = new NativeArray<int>(haloSize * haloSize, Allocator.Temp,
			                                   NativeArrayOptions.UninitializedMemory);

			var contMap = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);
			var pvMap   = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);
			var erosMap = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);
			var rivMap  = new NativeTexture2D<float>(new int2(haloSize, haloSize), Allocator.Temp);

			continentalnessNoise.GenUniformGrid2D(contMap, out _, originX, originZ, haloSize, haloSize, 1, 1, seed);
			peaksAndValleysNoise.GenUniformGrid2D(pvMap, out _, originX, originZ, haloSize, haloSize, 1, 1, seed);
			erosionNoise.GenUniformGrid2D(erosMap, out _, originX, originZ, haloSize, haloSize, 1, 1, seed);
			riverNoise.GenUniformGrid2D(rivMap, out _, originX, originZ, haloSize, haloSize, 1, 1, seed);

			for (var z = 0; z < haloSize; z++)
			for (var x = 0; x < haloSize; x++)
				haloHeights[x + z * haloSize] = ComputeHeight(
				                                              contMap[x, z], pvMap[x, z], erosMap[x, z], rivMap[x, z],
				                                              ref continentalnessHeight, ref erosionCurve,
				                                              ref peaksAndValleysCurve);

			contMap.Dispose();
			pvMap.Dispose();
			erosMap.Dispose();
			rivMap.Dispose();
		}
		
		[BurstCompile]
		public static void GenerateCaveMap(
			out NativeTexture3D<float> caveMap,
			ref FastNoise              caveNoise,
			ref int3                   chunkWorldPos,
			int                        chunkSize,
			int                        seed)
		{
			caveMap = new NativeTexture3D<float>(
			                                     new int3(chunkSize, chunkSize, chunkSize), Allocator.TempJob);
			caveNoise.GenUniformGrid3D(caveMap, out _,
			                           chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z,
			                           chunkSize, chunkSize, chunkSize, 1, 1, 1, seed);
		}
		
		[BurstCompile]
		private static int ComputeHeight(
			float           cont,
			float           pv,
			float           eros,
			float           river,
			ref NativeCurve contSpline,
			ref NativeCurve erosSpline,
			ref NativeCurve pvSpline)
		{
			var baseHeight    = contSpline.Evaluate(cont);
			var erosionFactor = erosSpline.Evaluate(eros);
			var peaksBonus    = pvSpline.Evaluate(pv);

			// Combine: peaks only matter where erosion is low
			var h = baseHeight + (erosionFactor * peaksBonus);

			// River carving: abs(FBm) near-zero lines = river centerlines
			if (!(river < RIVER_HALF_WIDTH) || !(baseHeight > -20f)) return (int)math.round(h);
			var t     = 1f - river / RIVER_HALF_WIDTH;      // 1 at center, 0 at edge
			var carve = t * t * MAX_RIVER_CARVE;                 // quadratic V-profile
			carve *= math.saturate(1f - erosionFactor * 0.6f); // no deep rivers through mountains
			h     -= carve;

			return (int)math.round(h);
		}
		
		[BurstCompile]
		public static ushort ClassifyVoxel(int worldY, int terrainHeight)
		{
			if (worldY > terrainHeight)
				return worldY <= 0 ? (ushort)4 : (ushort)0; // water below sea level, air above

			var depth      = terrainHeight - worldY;
			var underwater = terrainHeight < 0;              // fully submerged column
			var beach      = terrainHeight is >= 0 and <= 5; // at/just above sea level

			return depth switch
			       {
				       0    => underwater || beach ? (ushort)5 : (ushort)3,
				       <= 4 => underwater || beach ? (ushort)5 : (ushort)2,
				       _    => 1
			       };
		}
	}
}