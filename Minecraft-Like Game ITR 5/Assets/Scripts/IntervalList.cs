using System;
using System.Text;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace PatataGames
{
	[BurstCompile]
	public struct IntervalList
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

		private UnsafeList<Node> Internal;

		public int Length;

		public int CompressedLength => Internal.Length;

		public IntervalList(int capacity, Allocator allocator)
		{
			Internal = new UnsafeList<Node>(capacity, allocator);
			Length = 0;
		}

		public IntervalList(INativeList<int> list, int capacity, Allocator allocator)
		{
			Internal = new UnsafeList<Node>(capacity, allocator);
			Length = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (list == null || list.Length == 0) throw new NullReferenceException("List is null or empty");
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
		public void Dispose()
		{
			Internal.Dispose();
		}

		public int NodeIndex(int index) => BinarySearch(index);

		public void AddInterval(int id, int count)
		{
			Length += count;
			Internal.Add(new Node(id, Length));
		}

		public int Get(int index)
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (index >= Length) throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif
			return Internal[BinarySearch(index)].ID;
		}

		public bool Set(int index, int id)
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (index >= Length) throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif

			var node_index = BinarySearch(index);

			var block = Internal[node_index].ID;

			if (block == id) return false; // No Change

			var (left_item, left_node_index) = LeftOf(index, node_index);
			var (right_item, right_node_index) = RightOf(index, node_index);

			// Nodes are returned by value, so we need to update them back in the array

			if (id == left_item && id == right_item)
			{ // [X,A,X] -> [X,X,X]
				var left_node = Internal[left_node_index];

				left_node.Count = Internal[right_node_index].Count;

				Internal[left_node_index] = left_node;

				Internal.RemoveRange(node_index, 2);
			}
			else if (id == left_item)
			{ // [X,A,A,Y] -> [X,X,A,Y]
				var left_node = Internal[left_node_index]; // This is returned by value
				var node = Internal[node_index];

				left_node.Count++;

				Internal[left_node_index] = left_node;

				if (left_node.Count == node.Count) Internal.RemoveRange(node_index, 1); // [X,A,Y] -> [X,X,Y]
			}
			else if (id == right_item)
			{ // [X,A,A,Y] -> [X,A,Y,Y]
				var left_node = Internal[left_node_index];
				var node = Internal[node_index];

				node.Count--;

				Internal[node_index] = node;

				if (left_node.Count == node.Count) Internal.RemoveRange(node_index, 1); // [X,A,Y] -> [X,Y,Y]
			}
			else
			{ // No Coalesce
				if (block == left_item && block == right_item)
				{ // [X,X,X] -> [X,A,X]
					Internal.InsertRange(node_index, 2);

					var left_node = Internal[node_index];
					var node = Internal[node_index + 1];
					var right_node = Internal[node_index + 2];

					left_node.Count = index;

					node.ID = id;
					node.Count = index + 1;

					right_node.ID = left_node.ID;

					Internal[node_index] = left_node;
					Internal[node_index + 1] = node;
					Internal[node_index + 2] = right_node;
				}
				else if (block != left_item && block == right_item)
				{ // [X,Y,Y] -> [X,A,Y]
					Internal.InsertRange(node_index, 1);

					var node = Internal[node_index];

					node.ID = id;
					node.Count = Internal[left_node_index].Count + 1;

					Internal[node_index] = node;
				}
				else if (block == left_item && block != right_item)
				{ // [X,X,Y] -> [X,A,Y]
					Internal.InsertRange(node_index, 1);

					var node = Internal[node_index + 1];
					var left_node = Internal[left_node_index];

					node.ID = id;
					node.Count = left_node.Count;

					left_node.Count--;

					Internal[node_index + 1] = node;
					Internal[left_node_index] = left_node;
				}
				else
				{ // [X,Y,X] -> [X,A,X] or [X,Y,Z] -> [X,A,Z]
					var node = Internal[node_index];

					node.ID = id;

					Internal[node_index] = node;
				}
			}

			return true;
		}

		public int LeftOf(int index)
		{
			return LeftOf(index, NodeIndex(index)).Item1;
		}

		public int RightOf(int index)
		{
			return RightOf(index, NodeIndex(index)).Item1;
		}

		private (int, int) LeftOf(int index, int node_index)
		{
			if (node_index == 0)
			{ // First Node
				return index == 0 ? (-1, -1) : (Internal[node_index].ID, node_index);
			}

			var left = Internal[node_index - 1];
			var node = Internal[node_index];

			return index - 1 < left.Count ? (left.ID, node_index - 1) : (node.ID, node_index);
		}

		private (int, int) RightOf(int index, int node_index)
		{
			if (node_index == CompressedLength - 1)
			{ // Last Node
				return index == Length - 1 ? (-1, -1) : (Internal[node_index].ID, node_index);
			}

			var right = Internal[node_index + 1];
			var node = Internal[node_index];

			return index + 1 >= node.Count ? (right.ID, node_index + 1) : (node.ID, node_index);
		}

		private int BinarySearch(int index)
		{
			var min = 0;
			var max = Internal.Length;

			while (min <= max)
			{
				var mid = (max + min) / 2;
				var count = Internal[mid].Count;

				if (index == count) return mid + 1;

				if (index < count) max = mid - 1;
				else min = mid + 1;
			}

			return min;
		}

		public override string ToString()
		{
			var sb = new StringBuilder($"Length: {Length}, Compressed: {CompressedLength}\n");

			foreach (var node in Internal)
			{
				sb.AppendLine($"[Data: {node.ID}, Count: {node.Count}]");
			}

			return sb.ToString();
		}
	}
}