namespace NativeTexture
{
  using System;
  using System.Diagnostics;
  using System.Runtime.CompilerServices;
  using Unity.Burst;
  using Unity.Collections;
  using Unity.Collections.LowLevel.Unsafe;
  using Unity.Jobs;
  using Unity.Mathematics;
  using Utilities;

  /// <summary>
  /// A native container for 4D texture data with efficient memory management and direct data access.
  /// Useful for 4D noise fields, animation volumes, parameterized textures, etc.
  /// Implements INativeTexture for 4D textures and IDisposable for proper resource cleanup.
  /// </summary>
  /// <typeparam name="T">The unmanaged type of the texture data (e.g., float).</typeparam>
  [DebuggerDisplay("Length = {m_Length}")]
  [NativeContainer]
  [NativeContainerSupportsMinMaxWriteRestriction]
  [NativeContainerSupportsDeallocateOnJobCompletion]
  public struct NativeTexture4D<T> : INativeTexture<int4, T>, INativeDisposable
    where T : unmanaged
  {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal AtomicSafetyHandle m_Safety;
    internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<
      NativeTexture4D<T>
    >();
#endif

    // Support for IJobParallelFor min/max restrictions
#pragma warning disable IDE1006 // Naming Styles
    [NativeDisableUnsafePtrRestriction]
    internal unsafe void* m_Buffer;

    internal int m_Length;
    internal int m_MinIndex;
    internal int m_MaxIndex;

    // Allocator type
    internal Allocator m_AllocatorLabel;
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// Gets the width (X dimension) of the 4D texture.
    /// </summary>
    public readonly int Width => Resolution.x;

    /// <summary>
    /// Gets the height (Y dimension) of the 4D texture.
    /// </summary>
    public readonly int Height => Resolution.y;

    /// <summary>
    /// Gets the depth (Z dimension) of the 4D texture.
    /// </summary>
    public readonly int Depth => Resolution.z;

    /// <summary>
    /// Gets the size of the fourth dimension (W) of the 4D texture.
    /// </summary>
    public readonly int WSize => Resolution.w;

    /// <summary>
    /// Gets the product of width and height dimensions, used for coordinate calculations.
    /// </summary>
    public readonly int widthXHeight;

    /// <summary>
    /// Gets the product of width, height, and depth dimensions, used for coordinate calculations.
    /// </summary>
    public readonly int widthXHeightXDepth;

    /// <summary>
    /// Gets whether the native texture has been created and initialized.
    /// </summary>
    public readonly unsafe bool IsCreated => m_Buffer != null;

    #region Constructors

    /// <summary>
    /// Creates a new NativeTexture4D with the specified 4D resolution.
    /// </summary>
    /// <param name="resolution">The 4D dimensions (width, height, depth, w) of the texture.</param>
    /// <param name="allocator">The allocator to use for the texture data.</param>
    public unsafe NativeTexture4D(int4 resolution, Allocator allocator)
    {
      Resolution = resolution;
      widthXHeight = resolution.x * resolution.y;
      widthXHeightXDepth = resolution.x * resolution.y * resolution.z;

      // Allocate memory directly
      int length = resolution.x * resolution.y * resolution.z * resolution.w;
      long size = (long)UnsafeUtility.SizeOf<T>() * length;

      CheckAllocateArguments(length, allocator);

      m_Buffer = UnsafeUtility.MallocTracked(size, UnsafeUtility.AlignOf<T>(), allocator, 0);
      m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      // Initialize safety handles
      m_Safety = AtomicSafetyHandle.Create();
      InitStaticSafetyId(ref m_Safety);
#endif

      // Set min/max indices for job system
      m_MinIndex = 0;
      m_MaxIndex = length - 1;
      m_Length = length;
    }

    #endregion

    #region Safety Handling

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private static void CheckAllocateArguments(int length, Allocator allocator)
    {
      if (allocator <= Allocator.None)
        throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");

      if (allocator >= Allocator.FirstUserIndex)
        throw new ArgumentException(
          "Use CollectionHelper.CreateNativeArray for custom allocator",
          "allocator"
        );

      if (length < 0)
        throw new ArgumentOutOfRangeException("length", "Length must be >= 0");
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    /// <summary>
    /// Initialize static safety ID for this container type
    /// </summary>
    [BurstDiscard]
    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private static void InitStaticSafetyId(ref AtomicSafetyHandle handle)
    {
      if (s_staticSafetyId.Data == 0)
        s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<NativeTexture4D<T>>();

      AtomicSafetyHandle.SetStaticSafetyId(ref handle, s_staticSafetyId.Data);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private readonly void CheckElementReadAccess(int index)
    {
      if (index < m_MinIndex || index > m_MaxIndex)
        FailOutOfRangeError(index);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private readonly void CheckElementWriteAccess(int index)
    {
      if (index < m_MinIndex || index > m_MaxIndex)
        FailOutOfRangeError(index);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private readonly void FailOutOfRangeError(int index)
    {
      if (index < m_Length && (m_MinIndex != 0 || m_MaxIndex != m_Length - 1))
        throw new IndexOutOfRangeException(
          $"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in NativeTexture4D.\n"
            + "NativeTexture4D are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job."
        );

      throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
    }

    #endregion

    #region INativeTexture API

    /// <summary>
    /// Gets the 4D resolution (width, height, depth, w) of the texture.
    /// </summary>
    public int4 Resolution { get; }

    /// <summary>
    /// Gets the total number of elements in the 4D texture.
    /// </summary>
    public readonly int Length
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => m_Length;
    }

    /// <summary>
    /// Conventional texel size.
    /// </summary>
    public readonly float4 TexelSize => new(Resolution.xy, math.rcp(Resolution.xy));

    /// <summary>
    /// Gets or sets the texture value at the specified linear index.
    /// </summary>
    /// <param name="index">The linear index of the element.</param>
    /// <returns>The value at the specified index.</returns>
    public unsafe T this[int index]
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get
      {
        CheckElementReadAccess(index);
        return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
      }
      [WriteAccessRequired]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      set
      {
        CheckElementWriteAccess(index);
        UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
      }
    }

    /// <summary>
    /// Gets or sets the texture value at the specified 4D coordinate.
    /// </summary>
    /// <param name="coord">The 4D coordinate (x, y, z, w) of the element.</param>
    /// <returns>The value at the specified coordinate.</returns>
    public unsafe T this[int4 coord]
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get
      {
        int index = coord.ToIndex(widthXHeightXDepth, widthXHeight, Width);
        CheckElementReadAccess(index);
        return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
      }
      [WriteAccessRequired]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      set
      {
        int index = coord.ToIndex(widthXHeightXDepth, widthXHeight, Width);
        CheckElementWriteAccess(index);
        UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
      }
    }

    /// <summary>
    /// Reads the texture value at the specified 4D coordinate.
    /// </summary>
    /// <param name="coord">The 4D coordinate of the element.</param>
    /// <returns>The value at the specified coordinate.</returns>
    public unsafe T Load(int4 coord)
    {
      int index = coord.ToIndex(widthXHeightXDepth, widthXHeight, Width);
      CheckElementReadAccess(index);
      return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
    }

    /// <summary>
    /// Reads the texture value at the specified linear index and outputs the corresponding 4D coordinate.
    /// </summary>
    /// <param name="index">The linear index of the element.</param>
    /// <param name="coord">Outputs the 4D coordinate corresponding to the linear index.</param>
    /// <returns>The value at the specified index.</returns>
    public unsafe T Load(int index, out int4 coord)
    {
      CheckElementReadAccess(index);
      coord = index.ToCoord(widthXHeightXDepth, widthXHeight, Width);
      return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
    }

    /// <summary>
    /// Returns the underlying texture data as a NativeArray.
    /// </summary>
    /// <returns>A NativeArray containing the texture data.</returns>
    public readonly unsafe NativeArray<T> AsArray()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
      AtomicSafetyHandle arraySafety = m_Safety;
      AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

      // Create a NativeArray that references the same memory
      NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
        m_Buffer,
        m_Length,
        Allocator.None
      );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif

      return array;
    }

    /// <summary>
    /// Returns an array that aliases this texture for use in a job. When you use this array in a job
    /// that modifies the data, you can schedule job which depends on that job the deferred array.
    /// </summary>
    /// <returns>A NativeArray that can be used safely in chained jobs.</returns>
    public unsafe NativeArray<T> AsDeferredJobArray()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif

      // Create a NativeArray that references the same memory
      NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
        m_Buffer,
        m_Length,
        Allocator.None
      );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
#endif

      return array;
    }

    /// <summary>
    /// Gets an unsafe pointer to the underlying texture data.
    /// </summary>
    /// <returns>An unsafe pointer to the texture data.</returns>
    public readonly unsafe void* GetUnsafePtr()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
      return m_Buffer;
    }

    /// <summary>
    /// Gets an unsafe read-only pointer to the underlying texture data.
    /// </summary>
    /// <returns>An unsafe read-only pointer to the texture data.</returns>
    public readonly unsafe void* GetUnsafeReadOnlyPtr()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
      return m_Buffer;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of the native texture resources.
    /// </summary>
    [WriteAccessRequired]
    public unsafe void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
      {
        AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
        AtomicSafetyHandle.Release(m_Safety);
      }
#endif

      if (IsCreated && m_AllocatorLabel > Allocator.None)
      {
        UnsafeUtility.FreeTracked(m_Buffer, m_AllocatorLabel);
        m_AllocatorLabel = Allocator.Invalid;
      }

      // Clear the reference to prevent double-disposal
      m_Buffer = null;
    }

    /// <summary>
    /// Schedules the disposal of native texture resources as a job.
    /// </summary>
    /// <param name="inputDeps">The JobHandle for any dependent jobs.</param>
    /// <returns>A JobHandle for the scheduled dispose job.</returns>
    [BurstCompile]
    private struct DisposeJob : IJob
    {
      [NativeDisableUnsafePtrRestriction]
      internal unsafe void* buffer;

      internal int length;
      internal Allocator allocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      internal AtomicSafetyHandle safety;
#endif

      public unsafe void Execute()
      {
        if (buffer != null && allocatorLabel > Allocator.None)
          UnsafeUtility.FreeTracked(buffer, allocatorLabel);
      }
    }

    public unsafe JobHandle Dispose(JobHandle inputDeps)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
        AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif

      if (!IsCreated)
        return inputDeps;

      // Schedule disposal job
      DisposeJob jobData = new()
      {
        buffer = m_Buffer,
        length = m_Length,
        allocatorLabel = m_AllocatorLabel,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        safety = m_Safety,
#endif
      };

      JobHandle handle = jobData.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
        AtomicSafetyHandle.Release(m_Safety);
#endif

      // Ensure we don't double-dispose by clearing the reference
      m_Buffer = null;
      m_AllocatorLabel = Allocator.Invalid;

      return handle;
    }

    #endregion

    /// <summary>
    /// Creates a read-only view of the native texture.
    /// </summary>
    /// <returns>A read-only view of the texture.</returns>
    [NativeContainerIsReadOnly]
    public readonly struct ReadOnly : INativeTexture<int4, T>
    {
      [NativeDisableUnsafePtrRestriction]
      internal readonly unsafe void* buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      internal readonly AtomicSafetyHandle safety;
#endif

      internal readonly int length;
      internal readonly int4 resolution;
      internal readonly int widthXHeight;
      internal readonly int widthXHeightXDepth;

      internal unsafe ReadOnly(NativeTexture4D<T> texture)
      {
        buffer = texture.m_Buffer;
        length = texture.Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        safety = texture.m_Safety;
#endif
        resolution = texture.Resolution;
        widthXHeight = texture.widthXHeight;
        widthXHeightXDepth = texture.widthXHeightXDepth;
      }

      /// <summary>
      /// Gets the 4D resolution (width, height, depth, w) of the texture.
      /// </summary>
      public int4 Resolution => resolution;

      public int Width => resolution.x;
      public int Height => resolution.y;
      public int Depth => resolution.z;
      public int WSize => resolution.w;

      public int Length => length;

      public readonly float4 TexelSize => new(resolution.xy, math.rcp(resolution.xy));

      /// <summary>
      /// Gets whether the native texture has been created and initialized.
      /// </summary>
      public unsafe bool IsCreated => buffer != null;

      public unsafe T this[int index]
      {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
          AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
          if ((uint)index >= (uint)length)
            throw new IndexOutOfRangeException(
              $"Index {index} is out of range (must be between 0 and {length - 1})."
            );

          return UnsafeUtility.ReadArrayElement<T>(buffer, index);
        }
        [WriteAccessRequired]
        set => throw new NotSupportedException("Cannot write to a ReadOnly view");
      }

      public unsafe T this[int4 coord]
      {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
          int index = coord.ToIndex(widthXHeightXDepth, widthXHeight, Width);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
          AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
          if ((uint)index >= (uint)length)
            throw new IndexOutOfRangeException(
              $"Index {index} is out of range (must be between 0 and {length - 1})."
            );

          return UnsafeUtility.ReadArrayElement<T>(buffer, index);
        }
        [WriteAccessRequired]
        set => throw new NotSupportedException("Cannot write to a ReadOnly view");
      }

      public unsafe T Load(int4 coord)
      {
        int index = coord.ToIndex(widthXHeightXDepth, widthXHeight, Width);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
        if ((uint)index >= (uint)length)
          throw new IndexOutOfRangeException(
            $"Index {index} is out of range (must be between 0 and {length - 1})."
          );

        return UnsafeUtility.ReadArrayElement<T>(buffer, index);
      }

      public unsafe T Load(int pixelIndex, out int4 coord)
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
        if ((uint)pixelIndex >= (uint)length)
          throw new IndexOutOfRangeException(
            $"Index {pixelIndex} is out of range (must be between 0 and {length - 1})."
          );

        coord = pixelIndex.ToCoord(widthXHeightXDepth, widthXHeight, Width);
        return UnsafeUtility.ReadArrayElement<T>(buffer, pixelIndex);
      }

      /// <summary>
      /// Returns the underlying texture data as a NativeArray.
      /// </summary>
      /// <returns>A NativeArray containing the texture data.</returns>
      public unsafe NativeArray<T> AsArray()
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(safety);
        AtomicSafetyHandle arraySafety = safety;
        AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

        // Create a NativeArray from our buffer
        NativeArray<T> tempArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
          buffer,
          length,
          Allocator.None
        );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref tempArray, arraySafety);
#endif

        return tempArray;
      }

      public unsafe void* GetUnsafePtr() =>
        throw new NotSupportedException(
          "Cannot get unsafe pointer from ReadOnly view, use GetUnsafeReadOnlyPtr instead"
        );

      public unsafe void* GetUnsafeReadOnlyPtr()
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
        return buffer;
      }
    }

    /// <summary>
    /// Returns a read-only view of this texture.
    /// </summary>
    /// <returns>A read-only view of the texture.</returns>
    public ReadOnly AsReadOnly() => new(this);
  }
}
