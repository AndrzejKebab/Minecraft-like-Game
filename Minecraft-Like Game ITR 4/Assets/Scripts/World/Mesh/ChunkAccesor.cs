using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PatataStudio.World.Mesh
{
    [BurstCompile]
    public struct ChunkAccessor
    {
        private readonly NativeParallelHashMap<int3, Chunk>.ReadOnly chunks;
        private int3 chunkSize;

        public ChunkAccessor(NativeParallelHashMap<int3, Chunk>.ReadOnly chunks, int3 chunkSize)
        {
            this.chunks = chunks;
            this.chunkSize = chunkSize;
        }

        public int GetBlockInChunk(int3 chunkPos, int3 blockPos)
        {
            var key = int3.zero;

            for (var index = 0; index < 3; index++)
            {
                if (blockPos[index] >= 0 && blockPos[index] < chunkSize[index]) continue;

                key[index] += blockPos[index] % (chunkSize[index] - 1);
                blockPos[index] -= key[index] * chunkSize[index];
            }

            key *= chunkSize;

            return TryGetChunk(chunkPos + key, out var chunk) ? chunk.GetBlock(blockPos) : 0;
        }

        internal bool TryGetChunk(int3 pos, out Chunk chunk) => chunks.TryGetValue(pos, out chunk);
        internal bool ContainsChunk(int3 coord) => chunks.ContainsKey(coord);

        #region TryGet Neighbours
        public bool TryGetNeighborPX(int3 pos, out Chunk chunk)
        {
            var px = pos + new int3(1, 0, 0) * chunkSize;

            return chunks.TryGetValue(px, out chunk);
        }

        public bool TryGetNeighborPY(int3 pos, out Chunk chunk)
        {
            var py = pos + new int3(0, 1, 0) * chunkSize;

            return chunks.TryGetValue(py, out chunk);
        }

        public bool TryGetNeighborPZ(int3 pos, out Chunk chunk)
        {
            var pz = pos + new int3(0, 0, 1) * chunkSize;

            return chunks.TryGetValue(pz, out chunk);
        }

        public bool TryGetNeighborNX(int3 pos, out Chunk chunk)
        {
            var nx = pos + new int3(-1, 0, 0) * chunkSize;

            return chunks.TryGetValue(nx, out chunk);
        }

        public bool TryGetNeighborNY(int3 pos, out Chunk chunk)
        {
            var ny = pos + new int3(0, -1, 0) * chunkSize;

            return chunks.TryGetValue(ny, out chunk);
        }


        public bool TryGetNeighborNZ(int3 pos, out Chunk chunk)
        {
            var nz = pos + new int3(0, 0, -1) * chunkSize;

            return chunks.TryGetValue(nz, out chunk);
        }
        #endregion
    }
}