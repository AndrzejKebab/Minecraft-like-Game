namespace NativeTexture.Utilities
{
  using System.Runtime.CompilerServices;
  using Unity.Mathematics;

  /// <summary>
  /// Provides utility methods for converting between linear pixel indices and multi-dimensional coordinates.
  /// </summary>
  public static class IndexCoordUtils
  {
    /// <summary>
    /// Converts a linear pixel index to a 3-dimensional coordinate.
    /// </summary>
    /// <param name="pixelIndex">The linear index of the pixel.</param>
    /// <param name="widthXHeight">The product of width and height dimensions.</param>
    /// <param name="width">The width dimension of the texture.</param>
    /// <returns>The corresponding 3-dimensional coordinate (x, y, z).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int3 ToCoord(this int pixelIndex, int widthXHeight, int width)
    {
      int z = pixelIndex / widthXHeight;
      int remainderAfterZ = pixelIndex % widthXHeight;
      int y = remainderAfterZ / width;
      int x = remainderAfterZ % width;

      return new int3(x, y, z);
    }

    /// <summary>
    /// Converts a linear pixel index to a 2-dimensional coordinate.
    /// </summary>
    /// <param name="pixelIndex">The linear index of the pixel.</param>
    /// <param name="width">The width dimension of the texture.</param>
    /// <returns>The corresponding 2-dimensional coordinate (x, y).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 ToCoord(this int pixelIndex, int width)
    {
      int y = pixelIndex / width;
      int x = pixelIndex % width;

      return new int2(x, y);
    }

    /// <summary>
    /// Converts a 3-dimensional coordinate to a linear pixel index.
    /// </summary>
    /// <param name="id">The 3-dimensional coordinate (x, y, z).</param>
    /// <param name="widthXHeight">The product of width and height dimensions.</param>
    /// <param name="width">The width dimension of the texture.</param>
    /// <returns>The corresponding linear pixel index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(this int3 id, int widthXHeight, int width) =>
      (id.z * widthXHeight) + (id.y * width) + id.x;

    /// <summary>
    /// Converts a 2-dimensional coordinate to a linear pixel index.
    /// </summary>
    /// <param name="id">The 2-dimensional coordinate (x, y).</param>
    /// <param name="width">The width dimension of the texture.</param>
    /// <returns>The corresponding linear pixel index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(this int2 id, int width) => (id.y * width) + id.x;

    /// <summary>
    /// Converts a linear pixel index to a 4-dimensional coordinate.
    /// </summary>
    /// <param name="pixelIndex">The linear index of the pixel.</param>
    /// <param name="widthXHeightXDepth">The product of width, height, and depth dimensions.</param>
    /// <param name="widthXHeight">The product of width and height dimensions.</param>
    /// <param name="width">The width dimension of the texture.</param>
    /// <returns>The corresponding 4-dimensional coordinate (x, y, z, w).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int4 ToCoord(this int pixelIndex, int widthXHeightXDepth, int widthXHeight, int width)
    {
      int w = pixelIndex / widthXHeightXDepth;
      int remainder = pixelIndex % widthXHeightXDepth;
      int z = remainder / widthXHeight;
      int remainder2 = remainder % widthXHeight;
      int y = remainder2 / width;
      int x = remainder2 % width;

      return new int4(x, y, z, w);
    }

    /// <summary>
    /// Converts a 4-dimensional coordinate to a linear pixel index.
    /// </summary>
    /// <param name="id">The 4-dimensional coordinate (x, y, z, w).</param>
    /// <param name="widthXHeightXDepth">The product of width, height, and depth dimensions.</param>
    /// <param name="widthXHeight">The product of width and height dimensions.</param>
    /// <param name="width">The width dimension of the texture.</param>
    /// <returns>The corresponding linear pixel index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIndex(this int4 id, int widthXHeightXDepth, int widthXHeight, int width) =>
      (id.w * widthXHeightXDepth) + (id.z * widthXHeight) + (id.y * width) + id.x;

  }
}
