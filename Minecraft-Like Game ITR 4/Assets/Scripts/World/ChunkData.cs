using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime;

namespace PatataStudio.World
{
    [BurstCompile]
    public struct ChunkData
    {
        private readonly int3 chunkSize;
        private UnsafeIntervalTree data;

        public ChunkData(int3 chunkSize)
        {
            this.chunkSize = chunkSize;
            data = new UnsafeIntervalTree(128, Allocator.Persistent);
        }

        public void AddBlocks(int block, int count)
        {
            data.AddNode(block, count);
        }

        public void SetBlock(int block, int x, int y, int z)
        {
            data.Set(block, chunkSize.Flatten(x, y, z));
        }

        public int GetBlock(int x, int y, int z)
        {
            return data.Get(chunkSize.Flatten(x, y, z));
        }

        public int GetBlock(int3 pos)
        {
            return data.Get(chunkSize.Flatten(pos.x, pos.y, pos.z));
        }

        public void Dispose()
        {
            data.Dispose();
        }

        public override string ToString()
        {
            return $"Chunk Data : {data.ToString()}";
        }
    }
}