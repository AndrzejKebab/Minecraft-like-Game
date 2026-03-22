using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace _Project
{
    /// <summary>
    /// A Burst-compatible, unmanaged min-heap priority queue that integrates with Unity's
    /// NativeContainer safety system.
    /// </summary>
    /// <remarks>
    /// Elements with a lower priority <c>float</c> value are dequeued first.
    ///
    /// Operations:
    /// <list type="bullet">
    ///   <item><see cref="Enqueue"/> — O(log n)</item>
    ///   <item><see cref="Dequeue"/> / <see cref="Peek"/> — O(log n) / O(1)</item>
    ///   <item><see cref="Contains"/> / <see cref="Remove"/> / <see cref="UpdatePriority"/> — O(n) linear scan</item>
    ///   <item><see cref="Clear"/> — O(n)</item>
    /// </list>
    ///
    /// Must be disposed after use. Use <see cref="Dispose()"/> or schedule disposal
    /// via <see cref="Dispose(JobHandle)"/>.
    /// </remarks>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct NativePriorityQueue<T> : INativeDisposable
        where T : unmanaged
    {
        // ---------------------------------------------------------------------------
        // Internal state
        // ---------------------------------------------------------------------------

        [NativeDisableUnsafePtrRestriction]
        internal UnsafePriorityQueue<T>* m_Queue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_StaticSafetyId =
            SharedStatic<int>.GetOrCreate<NativePriorityQueue<T>>();
#endif

        // ---------------------------------------------------------------------------
        // Construction / destruction
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Initializes a new <see cref="NativePriorityQueue{T}"/> with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Starting number of node slots.</param>
        /// <param name="allocator">The allocator used for all internal memory.</param>
        public NativePriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativePriorityQueue<T>>(ref m_Safety, ref s_StaticSafetyId.Data);
#endif
            m_Queue  = UnsafePriorityQueue<T>.Alloc(allocator);
            *m_Queue = new UnsafePriorityQueue<T>(initialCapacity, allocator);
        }

        // ---------------------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------------------

        /// <summary>Whether the queue has been allocated and not yet disposed.</summary>
        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Queue != null && m_Queue->IsCreated;
        }

        /// <summary>The number of elements currently in the queue.</summary>
        public readonly int Count
        {
            get
            {
                CheckRead();
                return m_Queue->Count;
            }
        }

        /// <summary>Returns true if the queue contains no elements.</summary>
        public readonly bool IsEmpty
        {
            get
            {
                CheckRead();
                return m_Queue->IsEmpty;
            }
        }

        // ---------------------------------------------------------------------------
        // Core operations
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Adds <paramref name="value"/> to the queue at the given <paramref name="priority"/>. O(log n).
        /// </summary>
        /// <param name="value">The value to enqueue.</param>
        /// <param name="priority">Lower values are dequeued first.</param>
        public void Enqueue(T value, float priority)
        {
            CheckWrite();
            m_Queue->Enqueue(value, priority);
        }

        /// <summary>
        /// Removes and returns the element with the lowest priority value. O(log n).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public T Dequeue()
        {
            CheckWrite();
            return m_Queue->Dequeue();
        }

        /// <summary>
        /// Removes and outputs the front element. Returns false if the queue is empty.
        /// </summary>
        public bool TryDequeue(out T value)
        {
            CheckWrite();
            return m_Queue->TryDequeue(out value);
        }

        /// <summary>
        /// Returns the element with the lowest priority value without removing it. O(1).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public T Peek()
        {
            CheckRead();
            return m_Queue->Peek();
        }

        /// <summary>
        /// Returns the full node (value + priority) at the front of the queue. O(1).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public PriorityQueueNode<T> PeekNode()
        {
            CheckRead();
            return m_Queue->PeekNode();
        }

        /// <summary>
        /// Removes all elements. O(n).
        /// </summary>
        public void Clear()
        {
            CheckWrite();
            m_Queue->Clear();
        }

        /// <summary>
        /// Returns true if <paramref name="value"/> is present (byte-equality scan). O(n).
        /// </summary>
        public readonly bool Contains(T value)
        {
            CheckRead();
            return m_Queue->Contains(value);
        }

        /// <summary>
        /// Removes the first occurrence of <paramref name="value"/>. O(n).
        /// </summary>
        /// <returns>True if an element was found and removed.</returns>
        public bool Remove(T value)
        {
            CheckWrite();
            return m_Queue->Remove(value);
        }

        /// <summary>
        /// Updates the priority of the first occurrence of <paramref name="value"/> and
        /// restores heap order. O(n).
        /// </summary>
        /// <returns>True if the element was found and updated.</returns>
        public bool UpdatePriority(T value, float newPriority)
        {
            CheckWrite();
            return m_Queue->UpdatePriority(value, newPriority);
        }

        /// <summary>
        /// Validates the heap invariant. Useful for debug/editor checks. O(n).
        /// </summary>
        public readonly bool IsValidQueue()
        {
            CheckRead();
            return m_Queue->IsValidQueue();
        }

        // ---------------------------------------------------------------------------
        // Enumerator (heap order, not sorted)
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Returns an enumerator over the internal heap storage.
        /// Elements are <b>not</b> in priority order.
        /// </summary>
        public Enumerator GetEnumerator()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle ash = m_Safety;
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(ash);
            AtomicSafetyHandle.UseSecondaryVersion(ref ash);
            return new Enumerator(m_Queue, ash);
#else
            return new Enumerator(m_Queue);
#endif
        }

        /// <summary>
        /// An enumerator over a <see cref="NativePriorityQueue{T}"/>.
        /// Elements are in heap storage order, <b>not</b> sorted by priority.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct Enumerator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif
            [NativeDisableUnsafePtrRestriction]
            private readonly UnsafePriorityQueue<T>* m_Queue;
            private int m_Index;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal Enumerator(UnsafePriorityQueue<T>* queue, AtomicSafetyHandle safety)
            {
                m_Queue  = queue;
                m_Index  = -1;
                m_Safety = safety;
            }
#else
            internal Enumerator(UnsafePriorityQueue<T>* queue)
            {
                m_Queue = queue;
                m_Index = -1;
            }
#endif

            /// <summary>Advances to the next element.</summary>
            public bool MoveNext()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                m_Index++;
                return m_Index < m_Queue->Count;
            }

            /// <summary>Resets the enumerator.</summary>
            public void Reset() => m_Index = -1;

            /// <summary>Does nothing.</summary>
            public void Dispose() { }

            /// <summary>The current node (value + priority).</summary>
            public PriorityQueueNode<T> Current
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    return m_Queue->m_Nodes[m_Index];
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Dispose
        // ---------------------------------------------------------------------------

        /// <summary>Releases all resources (memory and safety handles).</summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
            if (!IsCreated)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            UnsafePriorityQueue<T>.Free(m_Queue);
            m_Queue = null;
        }

        /// <summary>
        /// Schedules a job that disposes this queue after <paramref name="inputDeps"/> completes.
        /// </summary>
        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
            if (!IsCreated)
                return inputDeps;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            JobHandle jobHandle = new NativePriorityQueueDisposeJob<T>
                                  {
                                      Data = new NativePriorityQueueDispose<T> { m_QueueData = m_Queue, m_Safety = m_Safety }
                                  }.Schedule(inputDeps);
            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new NativePriorityQueueDisposeJob<T>
            {
                Data = new NativePriorityQueueDispose<T> { m_QueueData = m_Queue }
            }.Schedule(inputDeps);
#endif
            m_Queue = null;
            return jobHandle;
        }

        // ---------------------------------------------------------------------------
        // Safety guards
        // ---------------------------------------------------------------------------

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }

    // ---------------------------------------------------------------------------
    // Dispose job infrastructure (mirrors NativeQueueDisposeJob)
    // ---------------------------------------------------------------------------

    [NativeContainer]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    internal unsafe struct NativePriorityQueueDispose<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        public UnsafePriorityQueue<T>* m_QueueData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        public void Dispose() => UnsafePriorityQueue<T>.Free(m_QueueData);
    }

    [BurstCompile]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    internal unsafe struct NativePriorityQueueDisposeJob<T> : IJob where T : unmanaged
    {
        public NativePriorityQueueDispose<T> Data;
        public void Execute() => Data.Dispose();
    }
}