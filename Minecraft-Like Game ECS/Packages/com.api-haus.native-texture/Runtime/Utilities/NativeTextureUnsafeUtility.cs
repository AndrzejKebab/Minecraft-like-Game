using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;

namespace NativeTexture.Utilities
{
  using System;
  using System.Diagnostics;
  using Unity.Collections;
  using Unity.Collections.LowLevel.Unsafe;
  using Unity.Mathematics;

  /// <summary>
  /// Contains unsafe methods for working with NativeTexture instances.
  /// Similar to Unity's NativeArrayUnsafeUtility.
  /// </summary>
  public static class NativeTextureUnsafeUtility
  {
    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private static void CheckConvertArguments<T>(int2 resolution)
      where T : unmanaged
    {
      if (resolution.x < 0 || resolution.y < 0)
        throw new ArgumentOutOfRangeException("resolution", "Resolution dimensions must be >= 0");

      // Verify T is unmanaged
      if (!IsUnmanaged<T>())
        throw new InvalidOperationException(
          $"{typeof(T)} used in NativeTexture2D<{typeof(T)}> must be unmanaged (contain no managed types)."
        );
    }

    /// <summary>
    /// Converts existing data to a NativeTexture2D instance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the texture.</typeparam>
    /// <param name="dataPointer">Pointer to the existing data.</param>
    /// <param name="resolution">The resolution (width, height) of the texture.</param>
    /// <param name="allocator">The allocator that was used to create the memory pointed to, or Allocator.None if memory is not owned by this container.</param>
    /// <returns>A NativeTexture2D that references the provided data.</returns>
    public static unsafe NativeTexture2D<T> ConvertExistingDataToNativeTexture2D<T>(
      void* dataPointer,
      int2 resolution,
      int mipCount = 1,
      Allocator allocator = Allocator.None
    )
      where T : unmanaged
    {
      CheckConvertArguments<T>(resolution);

      // Create a default struct
      NativeTexture2D<T> result = default;

      // Initialize the fields directly
      result.m_Buffer = dataPointer;
      result.Resolution = resolution;
      result.m_Length = resolution.x * resolution.y;
      result.m_AllocatorLabel = allocator;
      result.m_MinIndex = 0;
      result.m_MaxIndex = result.m_Length - 1;
      result.texturePtr = IntPtr.Zero;
      result.MipCount = mipCount;

      // Initialize safety handle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      result.m_Safety = AtomicSafetyHandle.Create();

      if (NativeTexture2D<T>.s_staticSafetyId.Data == 0)
        NativeTexture2D<T>.s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<
          NativeTexture2D<T>
        >();
      AtomicSafetyHandle.SetStaticSafetyId(
        ref result.m_Safety,
        NativeTexture2D<T>.s_staticSafetyId.Data
      );
#endif

      return result;
    }

    /// <summary>
    /// Gets an unsafe pointer to the underlying texture data.
    /// </summary>
    /// <typeparam name="T">The type of elements in the texture.</typeparam>
    /// <param name="texture">The texture to get the pointer from.</param>
    /// <returns>An unsafe pointer to the texture data.</returns>
    public static unsafe void* GetUnsafePtr<T>(this NativeTexture2D<T> texture)
      where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(texture.m_Safety);
#endif
      return texture.m_Buffer;
    }

    public static unsafe void* GetUnsafePtr<T>(this UnsafeTexture2D<T> texture)
      where T : unmanaged => texture.buffer;

    public static unsafe T ReadPixel<T>(this NativeTexture2D<T> texture, int2 pixelCoord)
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(NativeTexture2D<T>));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(texture.m_Safety);
#endif
      return ReadArrayElement<T>(GetUnsafePtr(texture), index);
    }

    public static unsafe void WritePixel<T>(
      ref this NativeTexture2D<T> texture,
      int2 pixelCoord,
      T pixel
    )
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(NativeTexture2D<T>));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(texture.m_Safety);
#endif
      WriteArrayElement(GetUnsafePtr(texture), index, pixel);
    }

    public static unsafe T ReadPixel<T>(this UnsafeTexture2D<T> texture, int2 pixelCoord)
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(UnsafeTexture2D<T>));

      return ReadArrayElement<T>(GetUnsafePtr(texture), index);
    }

    public static unsafe void WritePixel<T>(
      ref this UnsafeTexture2D<T> texture,
      int2 pixelCoord,
      T pixel
    )
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(UnsafeTexture2D<T>));

      WriteArrayElement(GetUnsafePtr(texture), index, pixel);
    }

    // --- NativeTexture3D ---

    public static unsafe void* GetUnsafePtr<T>(this NativeTexture3D<T> texture)
      where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(texture.m_Safety);
#endif
      return texture.m_Buffer;
    }

    public static unsafe void* GetUnsafePtr<T>(this UnsafeTexture3D<T> texture)
      where T : unmanaged => texture.buffer;

    public static unsafe T ReadPixel<T>(this UnsafeTexture3D<T> texture, int3 pixelCoord)
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.widthXHeight, texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(UnsafeTexture3D<T>));

      return ReadArrayElement<T>(GetUnsafePtr(texture), index);
    }

    public static unsafe void WritePixel<T>(
      ref this UnsafeTexture3D<T> texture,
      int3 pixelCoord,
      T pixel
    )
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.widthXHeight, texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(UnsafeTexture3D<T>));

      WriteArrayElement(GetUnsafePtr(texture), index, pixel);
    }

    // --- NativeTexture4D ---

    public static unsafe void* GetUnsafePtr<T>(this NativeTexture4D<T> texture)
      where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(texture.m_Safety);
#endif
      return texture.m_Buffer;
    }

    public static unsafe void* GetUnsafePtr<T>(this UnsafeTexture4D<T> texture)
      where T : unmanaged => texture.buffer;

    public static unsafe T ReadPixel<T>(this UnsafeTexture4D<T> texture, int4 pixelCoord)
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.widthXHeightXDepth, texture.widthXHeight, texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(UnsafeTexture4D<T>));

      return ReadArrayElement<T>(GetUnsafePtr(texture), index);
    }

    public static unsafe void WritePixel<T>(
      ref this UnsafeTexture4D<T> texture,
      int4 pixelCoord,
      T pixel
    )
      where T : unmanaged
    {
      int index = pixelCoord.ToIndex(texture.widthXHeightXDepth, texture.widthXHeight, texture.Width);
      if (index < 0 || index >= texture.Length)
        throw new ArgumentOutOfRangeException(nameof(pixelCoord), nameof(UnsafeTexture4D<T>));

      WriteArrayElement(GetUnsafePtr(texture), index, pixel);
    }
  }
}
