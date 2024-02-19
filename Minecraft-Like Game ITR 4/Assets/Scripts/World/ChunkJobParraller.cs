using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static PatataStudio.GameSettings;

namespace PatataStudio.World
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance,
        FloatMode = FloatMode.Fast,
        FloatPrecision = FloatPrecision.Low)]
    public struct ChunkJob : IJobParallelFor
    {
        [ReadOnly] public NoiseGenerator NoiseGenerator;
        [ReadOnly] public NativeList<int3> Jobs;

        [WriteOnly] public NativeParallelHashMap<int3, Chunk>.ParallelWriter Results;

        public void Execute(int index)
        {
            var position = Jobs[index];

            var chunk = GenerateChunkData(position);

            Results.TryAdd(position, chunk);
        }

        private Chunk GenerateChunkData(int3 position)
        {
            var data = new Chunk(position, ChunkSize);

            NativeArray<int> noise = NoiseGenerator.Get2DNoise(position);
            var currentBlock = GetBlock(position, noise[0]);

            var count = 0;

            for (var x = 0; x < ChunkSize; x++)
            for (var y = 0; y < ChunkSize; y++)
            for (var z = 0; z < ChunkSize; z++)
            {
                var block = GetBlock(new int3(x, y, z), noise[x * ChunkSize + z]);
                count = block == currentBlock ? ++count : 1;

                if (block == currentBlock) continue;

                data.AddBlocks(currentBlock, count);
                currentBlock = block;
                count = 1;
            }

            data.AddBlocks(currentBlock, count); // Finale interval

            return data;
        }

        private static ushort GetBlock(int3 position, int terrainHeight) // Temporary solution for testing
        {
            return position.y switch
            {
                _ when !IsVoxelInWorld(position, WorldSizeInVoxels) => 0,
                0 => 1,
                _ when position.y > terrainHeight => terrainHeight <= WorldHeight ? (ushort)5 : (ushort)0,
                _ when position.y == terrainHeight => 4,
                _ when position.y < terrainHeight && position.y > terrainHeight - 6 => 3,
                _ => 2
            };
        }

        private static bool IsVoxelInWorld(int3 position, int worldSizeInVoxels)
        {
            var halfWorldSize = new int3(worldSizeInVoxels * 0.5f);
            return math.all((position >= -halfWorldSize) & (position < halfWorldSize));
        }
    }
}