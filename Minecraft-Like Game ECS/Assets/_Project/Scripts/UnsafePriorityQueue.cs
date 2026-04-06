using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace _Project
{
	/// <summary>
	///     A node stored inside an UnsafePriorityQueue. Pairs an unmanaged value with its priority.
	/// </summary>
	[GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
	public struct PriorityQueueNode<T> where T : unmanaged
	{
		/// <summary>The stored value.</summary>
		public T Value;

		/// <summary>The priority of this node. Lower values are dequeued first.</summary>
		public float Priority;
	}

	/// <summary>
	///     An unmanaged, Burst-compatible min-heap priority queue backed by a <see cref="UnsafeList{T}" />.
	///     Nodes with a lower priority float value are dequeued first.
	/// </summary>
	/// <remarks>
	///     This is the unsafe, allocation-tracking layer. Prefer <see cref="NativePriorityQueue{T}" />
	///     for code that benefits from Unity's safety-handle checks.
	///     The heap is 0-indexed. For a node at index <c>i</c>:
	///     left child  = 2i + 1
	///     right child = 2i + 2
	///     parent      = (i - 1) >> 1
	/// </remarks>
	[GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
	public unsafe struct UnsafePriorityQueue<T> : IDisposable
		where T : unmanaged
	{
		// ---------------------------------------------------------------------------
		// Internal state
		// ---------------------------------------------------------------------------

		internal UnsafeList<PriorityQueueNode<T>> m_Nodes;

		// ---------------------------------------------------------------------------
		// Construction / destruction
		// ---------------------------------------------------------------------------

		/// <summary>
		///     Creates an UnsafePriorityQueue with an initial capacity.
		/// </summary>
		/// <param name="initialCapacity">Initial number of node slots to allocate.</param>
		/// <param name="allocator">The allocator to use for the internal list.</param>
		public UnsafePriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
		{
			m_Nodes = new UnsafeList<PriorityQueueNode<T>>(initialCapacity, allocator);
		}

		/// <summary>Allocates an UnsafePriorityQueue on the heap and returns a pointer to it.</summary>
		internal static UnsafePriorityQueue<T>* Alloc(AllocatorManager.AllocatorHandle allocator)
		{
			var ptr = (UnsafePriorityQueue<T>*)AllocatorManager.Allocate(
			                                                             allocator, sizeof(UnsafePriorityQueue<T>),
			                                                             UnsafeUtility.AlignOf<UnsafePriorityQueue<T>>());
			return ptr;
		}

		/// <summary>Disposes and frees an UnsafePriorityQueue that was created via <see cref="Alloc" />.</summary>
		internal static void Free(UnsafePriorityQueue<T>* queue)
		{
			if (queue == null)
				return;

			AllocatorManager.AllocatorHandle allocator = queue->m_Nodes.Allocator;
			queue->Dispose();
			AllocatorManager.Free(allocator, queue);
		}

		// ---------------------------------------------------------------------------
		// Properties
		// ---------------------------------------------------------------------------

		/// <summary>Whether the queue has been allocated (and not yet disposed).</summary>
		public readonly bool IsCreated
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_Nodes.IsCreated;
		}

		/// <summary>The number of elements currently in the queue.</summary>
		public readonly int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_Nodes.Length;
		}

		/// <summary>Returns true if the queue contains no elements.</summary>
		public readonly bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_Nodes.Length == 0;
		}

		// ---------------------------------------------------------------------------
		// Core heap operations
		// ---------------------------------------------------------------------------

		/// <summary>
		///     Adds <paramref name="value" /> to the queue with the given <paramref name="priority" />.
		///     O(log n).
		/// </summary>
		public void Enqueue(T value, float priority)
		{
			var node = new PriorityQueueNode<T> { Value = value, Priority = priority };
			m_Nodes.Add(node);
			CascadeUp(m_Nodes.Length - 1);
		}

		/// <summary>
		///     Removes and returns the element with the lowest priority value. O(log n).
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
		public T Dequeue()
		{
			CheckNotEmpty();

			T result = m_Nodes[0].Value;

			var last = m_Nodes.Length - 1;
			if (last == 0)
			{
				m_Nodes.RemoveAtSwapBack(0);
				return result;
			}

			// Move the last node to the root and sift it down.
			m_Nodes[0] = m_Nodes[last];
			m_Nodes.RemoveAtSwapBack(last);
			CascadeDown(0);
			return result;
		}

		/// <summary>
		///     Attempts to dequeue the front element. Returns false if the queue is empty.
		/// </summary>
		public bool TryDequeue(out T value)
		{
			if (m_Nodes.Length == 0)
			{
				value = default;
				return false;
			}

			value = Dequeue();
			return true;
		}

		/// <summary>
		///     Returns the element with the lowest priority value without removing it. O(1).
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
		public T Peek()
		{
			CheckNotEmpty();
			return m_Nodes[0].Value;
		}

		/// <summary>
		///     Returns the full node (value + priority) at the front of the queue without removing it. O(1).
		/// </summary>
		public PriorityQueueNode<T> PeekNode()
		{
			CheckNotEmpty();
			return m_Nodes[0];
		}

		/// <summary>
		///     Removes all elements. O(n).
		/// </summary>
		public void Clear()
		{
			m_Nodes.Clear();
		}

		/// <summary>
		///     Performs a linear scan and returns true if <paramref name="value" /> is present. O(n).
		/// </summary>
		public readonly bool Contains(T value)
		{
			for (var i = 0; i < m_Nodes.Length; i++)
				if (UnsafeUtility.MemCmp(
				                         UnsafeUtility.AddressOf(ref m_Nodes.ElementAt(i).Value),
				                         UnsafeUtility.AddressOf(ref value),
				                         sizeof(T)) == 0)
					return true;

			return false;
		}

		/// <summary>
		///     Removes the first occurrence of <paramref name="value" /> found via linear scan. O(n).
		/// </summary>
		/// <returns>True if an element was found and removed.</returns>
		public bool Remove(T value)
		{
			for (var i = 0; i < m_Nodes.Length; i++)
			{
				if (UnsafeUtility.MemCmp(
				                         UnsafeUtility.AddressOf(ref m_Nodes.ElementAt(i).Value),
				                         UnsafeUtility.AddressOf(ref value),
				                         sizeof(T)) != 0)
					continue;

				RemoveAt(i);
				return true;
			}

			return false;
		}

		/// <summary>
		///     Updates the priority of the first occurrence of <paramref name="value" /> found via linear
		///     scan and restores heap order. O(n).
		/// </summary>
		/// <returns>True if the element was found and its priority updated.</returns>
		public bool UpdatePriority(T value, float newPriority)
		{
			for (var i = 0; i < m_Nodes.Length; i++)
			{
				if (UnsafeUtility.MemCmp(
				                         UnsafeUtility.AddressOf(ref m_Nodes.ElementAt(i).Value),
				                         UnsafeUtility.AddressOf(ref value),
				                         sizeof(T)) != 0)
					continue;

				m_Nodes.ElementAt(i) = new PriorityQueueNode<T> { Value = value, Priority = newPriority };
				OnNodeUpdated(i);
				return true;
			}

			return false;
		}

		/// <summary>
		///     Returns an enumerator over a copy of the internal node list (not sorted).
		/// </summary>
		public Enumerator GetEnumerator()
		{
			return new Enumerator(ref this);
		}

		// ---------------------------------------------------------------------------
		// Heap helpers
		// ---------------------------------------------------------------------------

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CascadeUp(int index)
		{
			while (index > 0)
			{
				var parent = (index - 1) >> 1;
				if (m_Nodes[parent].Priority <= m_Nodes[index].Priority)
					break;

				// Swap child and parent.
				(m_Nodes[parent], m_Nodes[index]) = (m_Nodes[index], m_Nodes[parent]);

				index = parent;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CascadeDown(int index)
		{
			var count = m_Nodes.Length;
			while (true)
			{
				var leftChild  = (index << 1) + 1;
				var rightChild = leftChild + 1;

				if (leftChild >= count)
					break;

				// Find the child with the higher priority (lower float value).
				var swapTarget = index;

				if (m_Nodes[leftChild].Priority < m_Nodes[swapTarget].Priority)
					swapTarget = leftChild;

				if (rightChild < count && m_Nodes[rightChild].Priority < m_Nodes[swapTarget].Priority)
					swapTarget = rightChild;

				if (swapTarget == index)
					break;

				(m_Nodes[swapTarget], m_Nodes[index]) = (m_Nodes[index], m_Nodes[swapTarget]);

				index = swapTarget;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void OnNodeUpdated(int index)
		{
			if (index > 0)
			{
				var parent = (index - 1) >> 1;
				if (m_Nodes[index].Priority < m_Nodes[parent].Priority)
				{
					CascadeUp(index);
					return;
				}
			}

			CascadeDown(index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RemoveAt(int index)
		{
			var last = m_Nodes.Length - 1;
			if (index == last)
			{
				m_Nodes.RemoveAtSwapBack(last);
				return;
			}

			m_Nodes[index] = m_Nodes[last];
			m_Nodes.RemoveAtSwapBack(last);
			OnNodeUpdated(index);
		}

		// ---------------------------------------------------------------------------
		// Validation
		// ---------------------------------------------------------------------------

		/// <summary>
		///     Verifies that the heap invariant holds for every node. O(n). Useful in editor/debug.
		/// </summary>
		public readonly bool IsValidQueue()
		{
			var count = m_Nodes.Length;
			for (var i = 0; i < count; i++)
			{
				var left  = (i << 1) + 1;
				var right = left + 1;

				if (left < count && m_Nodes[left].Priority < m_Nodes[i].Priority) return false;
				if (right < count && m_Nodes[right].Priority < m_Nodes[i].Priority) return false;
			}

			return true;
		}

		// ---------------------------------------------------------------------------
		// IDisposable
		// ---------------------------------------------------------------------------

		/// <summary>Releases all unmanaged memory held by this queue.</summary>
		public void Dispose()
		{
			m_Nodes.Dispose();
		}

		// ---------------------------------------------------------------------------
		// Guard helpers
		// ---------------------------------------------------------------------------

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private readonly void CheckNotEmpty()
		{
			if (m_Nodes.Length == 0)
				throw new InvalidOperationException("Cannot operate on an empty NativePriorityQueue.");
		}

		// ---------------------------------------------------------------------------
		// Enumerator (heap order, not sorted)
		// ---------------------------------------------------------------------------

		/// <summary>
		///     An enumerator over the internal heap storage. Elements are NOT in priority order.
		/// </summary>
		public struct Enumerator
		{
			private UnsafeList<PriorityQueueNode<T>> m_Nodes;
			private int                              m_Index;

			internal Enumerator(ref UnsafePriorityQueue<T> queue)
			{
				m_Nodes = queue.m_Nodes;
				m_Index = -1;
			}

			/// <summary>Advances to the next element.</summary>
			public bool MoveNext()
			{
				m_Index++;
				return m_Index < m_Nodes.Length;
			}

			/// <summary>Resets the enumerator.</summary>
			public void Reset()
			{
				m_Index = -1;
			}

			/// <summary>Returns the current node.</summary>
			public PriorityQueueNode<T> Current => m_Nodes[m_Index];
		}
	}
}