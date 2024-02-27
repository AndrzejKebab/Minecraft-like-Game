using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime;

namespace PatataStudio.World.TerrainGeneration
{
	public enum NoiseType
	{
		Continentalness = 0,
		Erosion = 1,
		Weirdness = 2
	}

	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public readonly struct NoiseGenerator
	{
		private readonly uint seed;
		private readonly int size;

		public NoiseGenerator(uint seed, int size) 
		{
			this.seed = seed;
			this.size = size;
		}

		public NativeArray<float> GenerateWorldMap(NoiseType noiseType, NoiseData noiseData)
		{
			Random random = new(seed);
			int maxNoiseHeight = noiseData.Octaves;
			int minNoiseHeight = -noiseData.Octaves;

			var noiseMap = new NativeArray<float>(size * size, Allocator.Temp);

			for(byte x = 0; x < size; x++)
			{
				for(byte z = 0; z < size; z++)
				{
					float amplitude = 1f;
					float frequency = 1f;
					float noiseHeight = 0f;

					for(byte i = 0; i < noiseData.Octaves; i++)
					{
						float sampleX = x / noiseData.NoiseScale * frequency + random.NextFloat(-100000, 100000);
						float sampleZ = z / noiseData.NoiseScale * frequency + random.NextFloat(-100000, 100000);
						var sampleXZ = new float2(sampleX, sampleZ);

						noiseHeight = noise.snoise(sampleXZ) * amplitude;

						amplitude *= noiseData.Persistence;
						frequency *= noiseData.Lacunarity;
					}

					noiseMap.SetAtFlatIndex(size, x, z, noiseHeight);
				}
			}

			return FineTuneNoise(ref noiseMap, minNoiseHeight, maxNoiseHeight, noiseType);
		}

		private NativeArray<float> FineTuneNoise(ref NativeArray<float> noiseMap, int minNoiseHeight, int maxNoiseHeight, NoiseType noiseType)
		{
			for (byte x = 0; x < size; x++)
			{
				for (byte z = 0; z < size; z++)
				{
					switch (noiseType)
					{
						case NoiseType.Continentalness:
							return noiseMap;
						case NoiseType.Erosion:
							noiseMap.SetAtFlatIndex(size, x, z, math.unlerp(minNoiseHeight, maxNoiseHeight, noiseMap.GetAtFlatIndex(size, x, z)));
							break;
						case NoiseType.Weirdness:
							noiseMap.SetAtFlatIndex(size, x, z, math.abs(noiseMap.GetAtFlatIndex(size, x, z)));
							break;
					}
				}
			}

			return noiseMap;
		}
	}
}