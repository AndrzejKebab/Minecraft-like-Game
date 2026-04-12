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
  using UnityEngine;
  using Utilities;

  /// <summary>
  /// A native wrapper for Unity's Texture2D that provides 3D texture functionality with efficient memory management and direct data access.
  /// Implements INativeTexture for 3D textures and IDisposable for proper resource cleanup.
  /// </summary>
  /// <typeparam name="T">The unmanaged type of the texture data (e.g., float).</typeparam>
  [DebuggerDisplay("Length = {m_Length}")]
  [NativeContainer]
  [NativeContainerSupportsMinMaxWriteRestriction]
  [NativeContainerSupportsDeallocateOnJobCompletion]
  public struct NativeTexture3D<T> : INativeTexture<int3, T>, INativeDisposable
    where T : unmanaged
  {
    [ReadOnly]
    [NativeDisableUnsafePtrRestriction]
    internal IntPtr texturePtr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal AtomicSafetyHandle m_Safety;
    internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<
      NativeTexture3D<T>
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
    /// Gets the width (X dimension) of the 3D texture in pixels.
    /// </summary>
    public readonly int Width => Resolution.x;

    /// <summary>
    /// Gets the height (Y dimension) of the 3D texture in pixels.
    /// </summary>
    public readonly int Height => Resolution.y;

    /// <summary>
    /// Gets the depth (Z dimension) of the 3D texture in pixels.
    /// </summary>
    public readonly int Depth => Resolution.z;

    /// <summary>
    /// Gets the product of width and height dimensions, used for coordinate calculations.
    /// </summary>
    public readonly int widthXHeight;

    /// <summary>
    /// Gets whether the native texture has been created and initialized.
    /// </summary>
    public readonly unsafe bool IsCreated => m_Buffer != null;

    /// <summary>
    /// Gets whether the texture data points directly to a Unity Texture2D native pointer.
    /// Returns true if the texture is not using a Unity native pointer.
    /// </summary>
    public readonly bool IsUnityTexture2DPointer => texturePtr != IntPtr.Zero;

    #region Constructors

    /// <summary>
    /// Creates a NativeTexture3D from an existing Unity Texture2D, treating it as a 3D texture with specified resolution.
    /// </summary>
    /// <param name="texture">The source Unity Texture2D to wrap.</param>
    /// <param name="resolution">The 3D dimensions (width, height, depth) of the texture.</param>
    public unsafe NativeTexture3D(Texture2D texture, int3 resolution)
    {
      Resolution = resolution;
      widthXHeight = resolution.x * resolution.y;
      texturePtr = texture.GetNativeTexturePtr();

      // Get the raw texture data
      NativeArray<T> textureData = texture.GetRawTextureData<T>();
      m_Buffer = textureData.GetUnsafePtr();

      // We don't own this memory - Texture2D does
      m_AllocatorLabel = Allocator.None;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      // Initialize safety handles
      m_Safety = AtomicSafetyHandle.Create();
      InitStaticSafetyId(ref m_Safety);
#endif

      // Set min/max indices for job system
      m_MinIndex = 0;
      m_MaxIndex = (resolution.x * resolution.y * resolution.z) - 1;
      m_Length = resolution.x * resolution.y * resolution.z;
    }

    /// <summary>
    /// Creates a new NativeTexture3D with the specified 3D resolution.
    /// </summary>
    /// <param name="resolution">The 3D dimensions (width, height, depth) of the texture.</param>
    /// <param name="allocator">The allocator to use for the texture data.</param>
    public unsafe NativeTexture3D(int3 resolution, Allocator allocator)
    {
      Resolution = resolution;
      widthXHeight = resolution.x * resolution.y;
      texturePtr = IntPtr.Zero;

      // Allocate memory directly
      int length = resolution.x * resolution.y * resolution.z;
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
        s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<NativeTexture3D<T>>();

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
          $"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in NativeTexture3D.\n"
            + "NativeTexture3D are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job."
        );

      throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
    }

    #endregion

    #region INativeTexture API

    /// <summary>
    /// Gets the 3D resolution (width, height, depth) of the texture.
    /// </summary>
    public int3 Resolution { get; }

    /// <summary>
    /// Gets the total number of voxels in the 3D texture.
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
    /// Gets or sets the texture value at the specified linear voxel index.
    /// </summary>
    /// <param name="index">The linear index of the voxel.</param>
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
    /// Gets or sets the texture value at the specified 3D coordinate.
    /// </summary>
    /// <param name="coord">The 3D coordinate (x, y, z) of the voxel.</param>
    /// <returns>The value at the specified coordinate.</returns>
    public unsafe T this[int3 coord]
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get
      {
        int index = coord.ToIndex(widthXHeight, Width);
        CheckElementReadAccess(index);
        return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
      }
      [WriteAccessRequired]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      set
      {
        int index = coord.ToIndex(widthXHeight, Width);
        CheckElementWriteAccess(index);
        UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
      }
    }

    /// <summary>
    /// Reads the texture value at the specified 3D coordinate.
    /// </summary>
    /// <param name="coord">The 3D coordinate of the voxel.</param>
    /// <returns>The value at the specified coordinate.</returns>
    public unsafe T Load(int3 coord)
    {
      int index = coord.ToIndex(widthXHeight, Width);
      CheckElementReadAccess(index);
      return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
    }

    /// <summary>
    /// Reads the texture value at the specified linear index and outputs the corresponding 3D coordinate.
    /// </summary>
    /// <param name="index">The linear index of the voxel.</param>
    /// <param name="coord">Outputs the 3D coordinate corresponding to the linear index.</param>
    /// <returns>The value at the specified index.</returns>
    public unsafe T Load(int index, out int3 coord)
    {
      CheckElementReadAccess(index);
      coord = index.ToCoord(widthXHeight, Width);
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
    /// Applies the native 3D texture data to a Unity Texture2D object.
    /// If the texture pointers don't match, copies the data to the target texture.
    /// </summary>
    /// <param name="texture">The Texture2D object to apply data to.</param>
    /// <param name="updateMipmaps">Whether to update mipmaps after applying data.</param>
    /// <returns>The updated Texture2D object.</returns>
    [BurstDiscard]
    public readonly unsafe Texture2D ApplyTo(Texture2D texture, bool updateMipmaps = false)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

      if (texture.GetNativeTexturePtr() != texturePtr)
      {
        NativeArray<T> writeableTextureMemory = texture.GetRawTextureData<T>();

        // Copy our memory to the texture's memory
        UnsafeUtility.MemCpy(
          writeableTextureMemory.GetUnsafePtr(),
          m_Buffer,
          (long)UnsafeUtility.SizeOf<T>() * m_Length
        );
      }

      texture.Apply(updateMipmaps);
      return texture;
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
    /// Disposes of the native texture resources, including the raw texture data if it was created.
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

      if (!IsUnityTexture2DPointer && IsCreated && m_AllocatorLabel > Allocator.None)
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

      [ReadOnly]
      [NativeDisableUnsafePtrRestriction]
      internal IntPtr texturePtr;

      public unsafe void Execute()
      {
        if (texturePtr == IntPtr.Zero && buffer != null && allocatorLabel > Allocator.None)
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
        texturePtr = texturePtr,
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
    public readonly struct ReadOnly : INativeTexture<int3, T>
    {
      [NativeDisableUnsafePtrRestriction]
      internal readonly unsafe void* buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      internal readonly AtomicSafetyHandle safety;
#endif

      internal readonly int length;
      internal readonly int3 resolution;
      internal readonly int widthXHeight;
      internal readonly bool isUnityTexture2DPointer;

      [NativeDisableUnsafePtrRestriction]
      internal readonly IntPtr texturePtr;

      internal unsafe ReadOnly(NativeTexture3D<T> texture)
      {
        buffer = texture.m_Buffer;
        length = texture.Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        safety = texture.m_Safety;
#endif
        resolution = texture.Resolution;
        widthXHeight = texture.widthXHeight;
        isUnityTexture2DPointer = texture.IsUnityTexture2DPointer;
        texturePtr = texture.texturePtr;
      }

      /// <summary>
      /// Gets the 3D resolution (width, height, depth) of the texture.
      /// </summary>
      public int3 Resolution => resolution;

      public int Width => resolution.x;
      public int Height => resolution.y;
      public int Depth => resolution.z;

      public int Length => length;

      public readonly float4 TexelSize => new(resolution.xy, math.rcp(resolution.xy));

      /// <summary>
      /// Gets whether the native texture has been created and initialized.
      /// </summary>
      public unsafe bool IsCreated => buffer != null;

      /// <summary>
      /// Gets whether the texture data points directly to a Unity Texture2D native pointer.
      /// </summary>
      public bool IsUnityTexture2DPointer => isUnityTexture2DPointer;

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

      public unsafe T this[int3 coord]
      {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
          int index = coord.ToIndex(widthXHeight, Width);
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

      public unsafe T Load(int3 coord)
      {
        int index = coord.ToIndex(widthXHeight, Width);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
        if ((uint)index >= (uint)length)
          throw new IndexOutOfRangeException(
            $"Index {index} is out of range (must be between 0 and {length - 1})."
          );

        return UnsafeUtility.ReadArrayElement<T>(buffer, index);
      }

      public unsafe T Load(int pixelIndex, out int3 coord)
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(safety);
#endif
        if ((uint)pixelIndex >= (uint)length)
          throw new IndexOutOfRangeException(
            $"Index {pixelIndex} is out of range (must be between 0 and {length - 1})."
          );

        coord = pixelIndex.ToCoord(widthXHeight, Width);
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

      /// <summary>
      /// Applies the native texture data to a Unity Texture2D object.
      /// Cannot be used from a ReadOnly view as it would modify the texture.
      /// </summary>
      [BurstDiscard]
      public Texture2D ApplyTo(Texture2D texture, bool updateMipmaps = false) =>
        throw new NotSupportedException("Cannot apply texture data from a ReadOnly view");

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
