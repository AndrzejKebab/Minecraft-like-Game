using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime;

namespace PatataStudio.World
{
    [BurstCompile]
    public struct Chunk
    {
        public int3 Position { get; }
        public bool Dirty { get; private set; }

        private readonly int3 chunkSize;
        private UnsafeIntervalList data;

        public Chunk(int3 position, int3 chunkSize)
        {
            Dirty = false;
            Position = position;
            this.chunkSize = chunkSize;
            data = new UnsafeIntervalList(128, Allocator.Persistent);
        }

        public void AddBlocks(int block, int count)
        {
            data.AddInterval(block, count);
        }

        public bool SetBlock(int x, int y, int z, int block)
        {
            var result = data.Set(chunkSize.Flatten(x, y, z), block);
            if (result) Dirty = true;
            return result;
        }

        public bool SetBlock(int3 pos, int block)
        {
            var result = data.Set(chunkSize.Flatten(pos), block);
            if (result) Dirty = true;
            return result;
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
            return $"Pos : {Position}, Dirty : {Dirty}, Data : {data.ToString()}";
        }
    }
}