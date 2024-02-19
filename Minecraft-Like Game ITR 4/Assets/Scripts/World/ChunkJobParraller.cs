using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PatataStudio.World
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, 
        FloatMode = FloatMode.Fast,
        FloatPrecision = FloatPrecision.Low)]
    public struct ChunkJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkSize;
        //[ReadOnly] public NoiseProfile NoiseProfile;

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

            //var noise = NoiseProfile.GetNoise(position);
            //int currentBlock = GetBlock(ref noise);

            var count = 0;

            // Loop order should be same as flatten order for AddBlocks to work properly
            for (var x = 0; x < ChunkSize.x; x++)
            {
                for (var y = 0; y < ChunkSize.y; y++)
                {
                    for (var z = 0; z < ChunkSize.z; z++)
                    {
                        /*
                        noise = NoiseProfile.GetNoise(position + new int3(x, y, z));

                        var block = GetBlock(ref noise);

                        if (block == current_block)
                        {
                            count++;
                        }
                        else
                        {
                            data.AddBlocks(current_block, count);
                            current_block = block;
                            count = 1;
                        }
                        */
                    }
                }
            }

            //data.AddBlocks(currentBlock, count); // Finale interval

            return data;
        }

        //private static int GetBlock(ref NoiseValue noise)
        //{
        //    var Y = noise.Position.y;
//
        //    if (Y > noise.Height)
        //        return Y > noise.WaterLevel ? (int)AllocatorManager.Block.AIR : (int)AllocatorManager.Block.WATER;
        //    if (Y == noise.Height) return (int)AllocatorManager.Block.GRASS;
        //    if (Y <= noise.Height - 1 && Y >= noise.Height - 3) return (int)AllocatorManager.Block.DIRT;
//
        //    return (int)AllocatorManager.Block.STONE;
        //}
    }
}