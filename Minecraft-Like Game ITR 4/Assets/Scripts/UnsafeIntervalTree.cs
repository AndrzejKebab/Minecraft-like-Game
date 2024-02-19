using System;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UtilityLibrary.Core;

namespace PatataStudio
{
    [BurstCompile]
    public struct UnsafeIntervalList
    {
        private struct Node
        {
            public int ID;
            public int Count;

            public Node(int id, int count)
            {
                ID = id;
                Count = count;
            }
        }

        private UnsafeList<Node> @internal;

        public int Length;

        public int CompressedLength => @internal.Length;

        public UnsafeIntervalList(int capacity, Allocator allocator)
        {
            @internal = new UnsafeList<Node>(capacity, allocator);
            Length = 0;
        }

        public UnsafeIntervalList(INativeList<int> list, int capacity, Allocator allocator)
        {
            @internal = new UnsafeList<Node>(capacity, allocator);
            Length = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (list.IsNull() || list.Length == 0) throw new NullReferenceException("List is null or empty");
#endif

            var current = list[0];
            var count = 0;

            for (var i = 0; i < list.Length; i++)
            {
                var id = list[i];

                if (current == id)
                {
                    count++;
                }
                else
                {
                    AddInterval(current, count);
                    current = id;
                    count = 1;
                }
            }

            AddInterval(current, count);
        }

        public int NodeIndex(int index)
        {
            return BinarySearch(index);
        }

        public void AddInterval(int id, int count)
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
        
        public bool Set(int index, int id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (index >= Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif

            var nodeIndex = BinarySearch(index);

            var block = @internal[nodeIndex].ID;

            if (block == id) return false; // No Change

            var (leftItem, leftNodeIndex) = LeftOf(index, nodeIndex);
            var (rightItem, rightNodeIndex) = RightOf(index, nodeIndex);

            // Nodes are returned by value, so we need to update them back in the array

            if (id == leftItem && id == rightItem)
            {
                // [X,A,X] -> [X,X,X]
                var leftNode = @internal[leftNodeIndex];

                leftNode.Count = @internal[rightNodeIndex].Count;

                @internal[leftNodeIndex] = leftNode;

                @internal.RemoveRange(nodeIndex, 2);
            }
            else if (id == leftItem)
            {
                // [X,A,A,Y] -> [X,X,A,Y]
                var leftNode = @internal[leftNodeIndex]; // This is returned by value
                var node = @internal[nodeIndex];

                leftNode.Count++;

                @internal[leftNodeIndex] = leftNode;

                if (leftNode.Count == node.Count) @internal.RemoveRange(nodeIndex, 1); // [X,A,Y] -> [X,X,Y]
            }
            else if (id == rightItem)
            {
                // [X,A,A,Y] -> [X,A,Y,Y]
                var leftNode = @internal[leftNodeIndex];
                var node = @internal[nodeIndex];

                node.Count--;

                @internal[nodeIndex] = node;

                if (leftNode.Count == node.Count) @internal.RemoveRange(nodeIndex, 1); // [X,A,Y] -> [X,Y,Y]
            }
            else
            {
                // No Coalesce
                if (block == leftItem && block == rightItem)
                {
                    // [X,X,X] -> [X,A,X]
                    // Unity docs says that InsertRange creates duplicates of node at node_index but in the
                    // debugger I have seen junk values sometimes, so to be safe we set the values of
                    // each newly created node to the correct value.
                    @internal.InsertRange(nodeIndex, 2);

                    var leftNode = @internal[nodeIndex];
                    var node = @internal[nodeIndex + 1];
                    var rightNode = @internal[nodeIndex + 2];

                    leftNode.Count = index;

                    node.ID = id;
                    node.Count = index + 1;

                    rightNode.ID = leftNode.ID;

                    @internal[nodeIndex] = leftNode;
                    @internal[nodeIndex + 1] = node;
                    @internal[nodeIndex + 2] = rightNode;
                }
                else if (block != leftItem && block == rightItem)
                {
                    // [X,Y,Y] -> [X,A,Y]
                    @internal.InsertRange(nodeIndex, 1);

                    var node = @internal[nodeIndex];

                    node.ID = id;
                    node.Count = @internal[leftNodeIndex].Count + 1;

                    @internal[nodeIndex] = node;
                }
                else if (block == leftItem && block != rightItem)
                {
                    // [X,X,Y] -> [X,A,Y]
                    @internal.InsertRange(nodeIndex, 1);

                    var node = @internal[nodeIndex + 1];
                    var leftNode = @internal[leftNodeIndex];

                    node.ID = id;
                    node.Count = leftNode.Count;

                    leftNode.Count--;

                    @internal[nodeIndex + 1] = node;
                    @internal[leftNodeIndex] = leftNode;
                }
                else
                {
                    // [X,Y,X] -> [X,A,X] or [X,Y,Z] -> [X,A,Z]
                    var node = @internal[nodeIndex];

                    node.ID = id;

                    @internal[nodeIndex] = node;
                }
            }

            return true;
        }

        public int LeftOf(int index)
        {
            return LeftOf(index, NodeIndex(index)).Item1;
        }

        private (int, int) LeftOf(int index, int nodeIndex)
        {
            if (nodeIndex == 0)
                // First Node
                return index == 0 ? (-1, -1) : (@internal[nodeIndex].ID, nodeIndex);

            var left = @internal[nodeIndex - 1];
            var node = @internal[nodeIndex];

            return index - 1 < left.Count ? (left.ID, nodeIndex - 1) : (node.ID, nodeIndex);
        }

        public int RightOf(int index)
        {
            return RightOf(index, NodeIndex(index)).Item1;
        }

        private (int, int) RightOf(int index, int nodeIndex)
        {
            if (nodeIndex == CompressedLength - 1)
                // Last Node
                return index == Length - 1 ? (-1, -1) : (@internal[nodeIndex].ID, nodeIndex);

            var right = @internal[nodeIndex + 1];
            var node = @internal[nodeIndex];

            return index + 1 >= node.Count ? (right.ID, nodeIndex + 1) : (node.ID, nodeIndex);
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

        public void Dispose()
        {
            @internal.Dispose();
        }
    }
}