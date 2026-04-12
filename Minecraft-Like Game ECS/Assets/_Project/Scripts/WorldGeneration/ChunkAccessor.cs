using Unity.Collections;
using Unity.Mathematics;
using _Project.WorldGeneration.Blocks;

namespace _Project.WorldGeneration.Jobs
{
    public struct ChunkAccessor
    {
        [ReadOnly] public NativeArray<BlockState> Center;
        [ReadOnly] public NativeArray<BlockState> NeighborXNeg;
        [ReadOnly] public NativeArray<BlockState> NeighborXPos;
        [ReadOnly] public NativeArray<BlockState> NeighborYNeg;
        [ReadOnly] public NativeArray<BlockState> NeighborYPos;
        [ReadOnly] public NativeArray<BlockState> NeighborZNeg;
        [ReadOnly] public NativeArray<BlockState> NeighborZPos;

        public int ChunkSize;

        public BlockState GetBlockState(int x, int y, int z)
        {
            switch (x)
            {
                case >= 0 when x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize:
                    return Center[x | (y << 5) | (z << 10)];
                case < 0 when y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize:
                    return NeighborXNeg.IsCreated ? NeighborXNeg[(ChunkSize - 1) | (y << 5) | (z << 10)] : default;
            }

            if (x >= ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize)
                return NeighborXPos.IsCreated ? NeighborXPos[0 | (y << 5) | (z << 10)] : default;

            if (y < 0 && x >= 0 && x < ChunkSize && z >= 0 && z < ChunkSize)
                return NeighborYNeg.IsCreated ? NeighborYNeg[x | ((ChunkSize - 1) << 5) | (z << 10)] : default;
            if (y >= ChunkSize && x >= 0 && x < ChunkSize && z >= 0 && z < ChunkSize)
                return NeighborYPos.IsCreated ? NeighborYPos[x | 0 | (z << 10)] : default;

            if (z < 0 && x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize)
                return NeighborZNeg.IsCreated ? NeighborZNeg[x | (y << 5) | ((ChunkSize - 1) << 10)] : default;
            if (z >= ChunkSize && x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize)
                return NeighborZPos.IsCreated ? NeighborZPos[x | (y << 5) | 0] : default;

            return default; // Air for absolute corners
        }

        public BlockState GetBlockState(int3 pos) => GetBlockState(pos.x, pos.y, pos.z);
    }
}