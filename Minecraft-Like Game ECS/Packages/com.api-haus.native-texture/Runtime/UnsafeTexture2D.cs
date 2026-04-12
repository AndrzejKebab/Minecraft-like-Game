namespace NativeTexture
{
  using Unity.Mathematics;
  using Utilities;

  public static class UnsafeTexture2DFactory
  {
    public static unsafe UnsafeTexture2D<T> FromNativeTexture<T>(NativeTexture2D<T> texture)
      where T : unmanaged =>
      new()
      {
        resolution = texture.Resolution,
        buffer = NativeTextureUnsafeUtility.GetUnsafePtr(texture),
        length = texture.Length,
      };
  }

  public unsafe struct UnsafeTexture2D<T>
    where T : unmanaged
  {
    public int2 resolution; // (width, height)

    internal void* buffer; // row-major T*

    internal int length;

    /// <summary>
    /// Gets the width of the texture in pixels.
    /// </summary>
    public readonly int Width => resolution.x;

    /// <summary>
    /// Gets the height of the texture in pixels.
    /// </summary>
    public readonly int Height => resolution.y;

    /// <summary>
    /// Gets the total number of pixels in the texture.
    /// </summary>
    public readonly int Length => length;

    public bool IsCreated => buffer != null;

    public bool TryRead(int2 local, out T value)
    {
      if (!IsCreated)
      {
        value = default;
        return false;
      }

      value = this.ReadPixel(local);
      return true;
    }

    public bool TryWrite(int2 local, in T value)
    {
      if (!IsCreated)
        return false;

      this.WritePixel(local, value);
      return true;
    }
  }
}
