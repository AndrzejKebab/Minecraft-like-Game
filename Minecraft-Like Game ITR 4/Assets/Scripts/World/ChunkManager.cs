using System;
using System.Collections.Generic;
using PatataStudio.World.Mesh;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UtilityLibrary.Unity.Runtime;
using UtilityLibrary.Unity.Runtime.PriorityQueue;
using UtilityLibrary.Unity.Runtime.Values;

namespace PatataStudio.World
{
    public class ChunkManager
    {
        private readonly Dictionary<int3, Chunk> chunks;
        private readonly SimpleFastPriorityQueue<int3, int> queue;
        private NativeParallelHashMap<int3, Chunk> accessorMap;

        private readonly HashSet<int3> reMeshChunks;
        private readonly HashSet<int3> reCollideChunks;

        private int3 focus;
        private readonly int3 chunkSize;
        private readonly int chunkStoreSize;

        internal ChunkManager()
        {
            chunkSize = GameSettings.ChunkSize;
            chunkStoreSize = (GameSettings.ViewDistance + 2).CubedSize();

            reMeshChunks = new HashSet<int3>();
            reCollideChunks = new HashSet<int3>();

            chunks = new Dictionary<int3, Chunk>(chunkStoreSize);
            queue = new SimpleFastPriorityQueue<int3, int>();

            accessorMap = new NativeParallelHashMap<int3, Chunk>(
                216,
                Allocator.Persistent
            );
        }

        #region API

        /// <summary>
        ///     Set a block at a position
        /// </summary>
        /// <param name="block">Block Type</param>
        /// <param name="position">World Position</param>
        /// <param name="remesh">Regenerate Mesh and Collider ?</param>
        /// <returns>Operation Success</returns>
        public bool SetBlock(ushort block, Vector3Int position, bool remesh = true)
        {
            var chunkPos = Utils.GetChunkCoords(position);
            var blockPos = Utils.GetBlockIndex(position);

            if (!chunks.ContainsKey(chunkPos))
            {
                Debug.LogWarning($"Chunk : {chunkPos} not loaded");
                return false;
            }

            var chunk = chunks[chunkPos];

            var result = chunk.SetBlock(blockPos, block);

            chunks[chunkPos] = chunk;

            if (remesh && result) ReMeshChunks(new int3(position.x, position.y, position.z));

            return result;
        }

        public int ChunkCount() => chunks.Count;
        public bool IsChunkLoaded(int3 position) => chunks.ContainsKey(position);
        #endregion

        internal bool ShouldReMesh(int3 position) => reMeshChunks.Contains(position);
        internal bool ShouldReCollide(int3 position) => reCollideChunks.Contains(position);
        internal void RemoveChunk(int3 position) => chunks.Remove(position);

        internal void FocusUpdate(int3 focus)
        {
            this.focus = focus;

            foreach (var position in queue) queue.UpdatePriority(position, -(position - focus).SqrMagnitude());
        }

        internal void AddChunks(NativeParallelHashMap<int3, Chunk> chunks)
        {
            foreach (KeyValue<int3, Chunk> pair in chunks)
            {
                var position = pair.Key;
                var chunk = pair.Value;

                if (this.chunks.ContainsKey(chunk.Position))
                    throw new InvalidOperationException($"Chunk {position} already exists");

                if (queue.Count >= chunkStoreSize) this.chunks.Remove(queue.Dequeue());
                // if dirty save chunk
                this.chunks.Add(position, chunk);
                queue.Enqueue(position, -(position - focus).SqrMagnitude());
            }
        }

        internal ChunkAccessor GetAccessor(List<int3> positions)
        {
            accessorMap.Clear();

            foreach (var position in positions)
                for (var x = -1; x <= 1; x++)
                for (var z = -1; z <= 1; z++)
                for (var y = -1; y <= 1; y++)
                {
                    var pos = position + chunkSize.MemberMultiply(x, y, z);

                    if (!chunks.ContainsKey(pos))
                        // Anytime this exception is thrown, mesh building completely stops
                        throw new InvalidOperationException($"Chunk {pos} has not been generated");

                    if (!accessorMap.ContainsKey(pos)) accessorMap.Add(pos, chunks[pos]);
                }

            return new ChunkAccessor(accessorMap.AsReadOnly(), chunkSize);
        }

        internal bool ReMeshedChunk(int3 position)
        {
            if (!reMeshChunks.Contains(position)) return false;
            reMeshChunks.Remove(position);
            reCollideChunks.Add(position);

            return true;
        }

        internal bool ReCollideChunk(int3 position)
        {
            if (!reCollideChunks.Contains(position)) return false;
            reCollideChunks.Remove(position);
            return true;
        }

        private void ReMeshChunks(int3 blockPosition)
        {
            foreach (var dir in Utils.Directions)
                reMeshChunks.Add(Utils.GetChunkCoords(blockPosition + dir));
        }
        
        internal void Dispose()
        {
            accessorMap.Dispose();

            foreach (var (_, chunk) in chunks) chunk.Dispose();
        }
    }
}