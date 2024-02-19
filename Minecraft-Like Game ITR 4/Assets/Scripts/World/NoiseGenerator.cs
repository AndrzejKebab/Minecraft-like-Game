using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static PatataStudio.GameSettings;

namespace PatataStudio.World
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance,
        FloatMode = FloatMode.Fast,
        FloatPrecision = FloatPrecision.Low)]
    public readonly struct NoiseGenerator
    {
        private readonly Random random;
        private readonly float lacunarity;
        private readonly float persistence;
        private readonly byte octaves;
        private readonly float scale;

        public NoiseGenerator(uint seed, float scale = 50, byte octaves = 4, float persistence = .5f,
            float lacunarity = 2)
        {
            random = new Random(seed);
            this.scale = scale;
            this.octaves = octaves;
            this.persistence = persistence;
            this.lacunarity = lacunarity;
        }

        public NativeArray<int> Get2DNoise(int3 position)
        {
            var maxNoiseHeight = float.MinValue;
            var minNoiseHeight = float.MaxValue;

            var noiseMap = new NativeArray<float>(ChunkSize * ChunkSize, Allocator.Temp);

            for (var x = 0; x < ChunkSize; x++)
            for (var z = 0; z < ChunkSize; z++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (var i = 0; i < octaves; i++)
                {
                    var sampleX = position.x / scale * frequency + random.NextFloat();
                    var sampleZ = position.z / scale * frequency + random.NextFloat();
                    var sampleXZ = new float2(sampleX, sampleZ);

                    var perlinValue = noise.snoise(sampleXZ);

                    noiseHeight += perlinValue * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                if (noiseHeight > maxNoiseHeight)
                    maxNoiseHeight = noiseHeight;
                else if (noiseHeight < minNoiseHeight) minNoiseHeight = noiseHeight;

                noiseMap[x + z * ChunkSize] = noiseHeight;
            }

            return FineTuneNoise(ref noiseMap, minNoiseHeight, maxNoiseHeight);
        }

        private static NativeArray<int> FineTuneNoise(ref NativeArray<float> noiseMap, float minNoiseHeight,
            float maxNoiseHeight)
        {
            var result = new NativeArray<int>(noiseMap.Length, Allocator.Temp);
            for (var x = 0; x < ChunkSize; x++)
            for (var z = 0; z < ChunkSize; z++)
            {
                var temp = math.unlerp(minNoiseHeight, maxNoiseHeight, noiseMap[x + z * ChunkSize]);
                result[x + z * ChunkSize] = (int)math.floor(temp * WorldHeight + WaterHeight + 1);
            }

            return result;
        }
    }
}