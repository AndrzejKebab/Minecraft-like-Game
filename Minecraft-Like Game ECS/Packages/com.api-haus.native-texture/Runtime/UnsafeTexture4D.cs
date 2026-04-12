namespace NativeTexture
{
  using Unity.Mathematics;
  using Utilities;

  public static class UnsafeTexture4DFactory
  {
    public static unsafe UnsafeTexture4D<T> FromNativeTexture<T>(NativeTexture4D<T> texture)
      where T : unmanaged =>
      new()
      {
        resolution = texture.Resolution,
        buffer = NativeTextureUnsafeUtility.GetUnsafePtr(texture),
        length = texture.Length,
        widthXHeight = texture.widthXHeight,
        widthXHeightXDepth = texture.widthXHeightXDepth,
      };
  }

  public unsafe struct UnsafeTexture4D<T>
    where T : unmanaged
  {
    public int4 resolution; // (width, height, depth, w)

    internal void* buffer; // row-major T*

    internal int length;

    internal int widthXHeight;

    internal int widthXHeightXDepth;

    /// <summary>
    /// Gets the width of the texture.
    /// </summary>
    public readonly int Width => resolution.x;

    /// <summary>
    /// Gets the height of the texture.
    /// </summary>
    public readonly int Height => resolution.y;

    /// <summary>
    /// Gets the depth of the texture.
    /// </summary>
    public readonly int Depth => resolution.z;

    /// <summary>
    /// Gets the size of the fourth dimension.
    /// </summary>
    public readonly int WSize => resolution.w;

    /// <summary>
    /// Gets the total number of elements in the texture.
    /// </summary>
    public readonly int Length => length;

    public bool IsCreated => buffer != null;

    public bool TryRead(int4 local, out T value)
    {
      if (!IsCreated)
      {
        value = default;
        return false;
      }

      value = this.ReadPixel(local);
      return true;
    }

    public bool TryWrite(int4 local, in T value)
    {
      if (!IsCreated)
        return false;

      this.WritePixel(local, value);
      return true;
    }
  }
}
