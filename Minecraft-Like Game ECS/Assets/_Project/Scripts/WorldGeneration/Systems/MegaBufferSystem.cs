using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration.Systems
{[UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MegaBufferSystem : SystemBase
    {
        // Increased sizes for large view distances! (50M verts = ~800MB VRAM)
        public const int MAX_VERTICES = 50_000_000;
        public const int MAX_INDICES  = 75_000_000;

        public GraphicsBuffer VertexBuffer { get; private set; }
        public GraphicsBuffer IndexBuffer  { get; private set; }

        private FreeListAllocator vertexAllocator;
        private FreeListAllocator indexAllocator;

        protected override void OnCreate()
        {
            VertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_VERTICES, sizeof(uint) * 5);
            IndexBuffer  = new GraphicsBuffer(GraphicsBuffer.Target.Index, MAX_INDICES, sizeof(int));

            vertexAllocator = new FreeListAllocator(MAX_VERTICES, Allocator.Persistent);
            indexAllocator  = new FreeListAllocator(MAX_INDICES, Allocator.Persistent);
        }

        public bool Allocate(int vertexCount, int indexCount, out int vOffset, out int iOffset)
        {
            var vSuccess = vertexAllocator.Allocate(vertexCount, out vOffset);
            var iSuccess = indexAllocator.Allocate(indexCount, out iOffset);

            if (vSuccess && iSuccess) return true;

            // Rollback if one fails to prevent memory leaks
            if (vSuccess) vertexAllocator.Free(vOffset, vertexCount);
            if (iSuccess) indexAllocator.Free(iOffset, indexCount);

            vOffset = iOffset = -1;
            return false;
        }

        public void Free(int vOffset, int vCount, int iOffset, int iCount)
        {
            if (vCount > 0 && vOffset >= 0) vertexAllocator.Free(vOffset, vCount);
            if (iCount > 0 && iOffset >= 0) indexAllocator.Free(iOffset, iCount);
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            VertexBuffer?.Release();
            IndexBuffer?.Release();
            vertexAllocator.Dispose();
            indexAllocator.Dispose();
        }
    }

    public struct FreeListAllocator : IDisposable
    {
        private struct Block
        {
            public int Offset;
            public int Size;
            public int End => Offset + Size;
        }

        private NativeList<Block> freeBlocks;
        public int Capacity { get; private set; }

        public FreeListAllocator(int capacity, Allocator allocator)
        {
            Capacity   = capacity;
            freeBlocks = new NativeList<Block>(allocator);
            freeBlocks.Add(new Block { Offset = 0, Size = capacity });
        }

        public bool Allocate(int size, out int offset)
        {
            if (size <= 0) { offset = 0; return true; }

            for (var i = 0; i < freeBlocks.Length; i++)
            {
                Block block = freeBlocks[i];
                if (block.Size < size) continue;
                offset = block.Offset;
                if (block.Size == size) freeBlocks.RemoveAtSwapBack(i);
                else
                {
                    block.Offset  += size;
                    block.Size    -= size;
                    freeBlocks[i] =  block;
                }
                return true;
            }

            offset = -1;
            return false;
        }

        public void Free(int offset, int size)
        {
            if (size <= 0) return;
            var newBlock = new Block { Offset = offset, Size = size };
            var merged   = true;

            while (merged)
            {
                merged = false;
                for (var i = 0; i < freeBlocks.Length; i++)
                {
                    Block block = freeBlocks[i];
                    if (newBlock.End == block.Offset)
                    {
                        newBlock.Size += block.Size;
                        freeBlocks.RemoveAtSwapBack(i);
                        merged = true; break;
                    }
                    if (block.End == newBlock.Offset)
                    {
                        newBlock.Offset =  block.Offset;
                        newBlock.Size   += block.Size;
                        freeBlocks.RemoveAtSwapBack(i);
                        merged = true; break;
                    }
                }
            }
            freeBlocks.Add(newBlock);
        }

        public void Dispose()
        {
            if (freeBlocks.IsCreated) freeBlocks.Dispose();
        }
    }
}