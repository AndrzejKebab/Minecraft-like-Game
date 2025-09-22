using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime;

namespace PatataGames
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance,
                  FloatMode   = FloatMode.Fast,
                  FloatPrecision = FloatPrecision.Low)]
    public struct PopulateVoxelMapParallel : IJobFor
    {
        [NativeDisableContainerSafetyRestriction] [WriteOnly]
        public NativeParallelHashMap<int3, Chunk>.ParallelWriter ChunkMap;
        [NativeDisableContainerSafetyRestriction] [ReadOnly]
        public NativeParallelHashMap<int3, Chunk>.ReadOnly ChunkMapRW;

        [ReadOnly] public NativeList<int3> ChunksToUpdate;

        public void Execute(int index)
        {
            int3 chunkPos = ChunksToUpdate[index];
            PopulateChunk(chunkPos);
        }

        private void PopulateChunk(int3 chunkPos)
        {
            var chunk = ChunkMapRW[chunkPos];
            
            for (var x = 0; x < ChunkSystem.CHUNK_SIZE; x++)
            for (var y = 0; y < ChunkSystem.CHUNK_SIZE; y++)
            for (var z = 0; z < ChunkSystem.CHUNK_SIZE; z++)
            {
                var pos        = new int3(chunkPos.x + x, chunkPos.y + y, chunkPos.z + z);
                var noiseValue = noise.snoise(pos) * 512f + 128;
                chunk.VoxelMap.SetAtFlatIndex(ChunkSystem.CHUNK_SIZE, x, y, z,
                                              noiseValue > 256 ? (short)0 : (short)1);
            }

            ChunkMap.TryAdd(chunkPos, chunk);
        }
    }
}