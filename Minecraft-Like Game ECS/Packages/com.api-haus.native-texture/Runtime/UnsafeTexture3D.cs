namespace NativeTexture
{
  using Unity.Mathematics;
  using Utilities;

  public static class UnsafeTexture3DFactory
  {
    public static unsafe UnsafeTexture3D<T> FromNativeTexture<T>(NativeTexture3D<T> texture)
      where T : unmanaged =>
      new()
      {
        resolution = texture.Resolution,
        buffer = NativeTextureUnsafeUtility.GetUnsafePtr(texture),
        length = texture.Length,
        widthXHeight = texture.widthXHeight,
      };
  }

  public unsafe struct UnsafeTexture3D<T>
    where T : unmanaged
  {
    public int3 resolution; // (width, height, depth)

    internal void* buffer; // row-major T*

    internal int length;

    internal int widthXHeight;

    /// <summary>
    /// Gets the width of the texture in pixels.
    /// </summary>
    public readonly int Width => resolution.x;

    /// <summary>
    /// Gets the height of the texture in pixels.
    /// </summary>
    public readonly int Height => resolution.y;

    /// <summary>
    /// Gets the depth of the texture in pixels.
    /// </summary>
    public readonly int Depth => resolution.z;

    /// <summary>
    /// Gets the total number of voxels in the texture.
    /// </summary>
    public readonly int Length => length;

    public bool IsCreated => buffer != null;

    public bool TryRead(int3 local, out T value)
    {
      if (!IsCreated)
      {
        value = default;
        return false;
      }

      value = this.ReadPixel(local);
      return true;
    }

    public bool TryWrite(int3 local, in T value)
    {
      if (!IsCreated)
        return false;

      this.WritePixel(local, value);
      return true;
    }
  }
}
