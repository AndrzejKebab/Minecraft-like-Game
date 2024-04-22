using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static FastNoise;

namespace PatataStudio.World.TerrainGeneration
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public readonly struct NoiseGenerator
	{
		private readonly int seed;
		private readonly int size;
		private readonly NativeArray<IntPtr> intPtrs;

		public NoiseGenerator(int seed, int size, NativeArray<IntPtr> intPtrs) 
		{
			this.seed = seed;
			this.size = size;
			this.intPtrs = intPtrs;
		}

		public NativeArray<float> GenerateWorldMap(int3 position)
		{
			var noiseMap = new NativeArray<float>(size * size, Allocator.Temp);

			var minMax = GenUniformGrid2D(intPtrs[0], noiseMap, position.x, position.z, size, size, 0.001f, seed);

			return noiseMap;
		}
	}
}