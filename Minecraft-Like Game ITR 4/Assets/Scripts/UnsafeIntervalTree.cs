using System;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace PatataStudio
{
    [BurstCompile]
    public struct UnsafeIntervalTree
    {
        private struct Node
        {
            public readonly int ID;
            public readonly int Count;

            public Node(int id, int count)
            {
                ID = id;
                Count = count;
            }
        }

        private UnsafeList<Node> @internal;

        public int Length;

        public int CompressedLength => @internal.Length;

        public UnsafeIntervalTree(int capacity, Allocator allocator)
        {
            @internal = new UnsafeList<Node>(capacity, allocator);
            Length = 0;
        }

        public void Dispose()
        {
            @internal.Dispose();
        }

        public void AddNode(int id, int count)
        {
            Length += count;
            @internal.Add(new Node(id, Length));
        }

        public int Get(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (index >= Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif
            return @internal[BinarySearch(index)].ID;
        }

        public void Set(int index, int id)
        {
            // TODO : To Implement (REF: https://github.com/mikolalysenko/NodeMinecraftThing/blob/master/client/voxels.js)
        }

        private int BinarySearch(int index)
        {
            var min = 0;
            var max = @internal.Length;

            while (min <= max)
            {
                var mid = (max + min) / 2;
                var count = @internal[mid].Count;

                if (index == count) return mid + 1;

                if (index < count) max = mid - 1;
                else min = mid + 1;
            }

            return min;
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"Length: {Length}, Compressed: {CompressedLength}\n");

            foreach (var node in @internal) sb.AppendLine($"[Data: {node.ID}, Count: {node.Count}]");

            return sb.ToString();
        }
    }
}