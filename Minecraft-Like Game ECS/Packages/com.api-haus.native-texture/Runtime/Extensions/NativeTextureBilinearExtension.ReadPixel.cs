using static Unity.Mathematics.math;

namespace NativeTexture.Extensions
{
  using System.Runtime.CompilerServices;
  using Formats;
  using Unity.Mathematics;

  public static partial class NativeTextureSamplingExtension
  {
    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixel(this NativeTexture2D<float> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixel(this NativeTexture2D<float>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixel(this NativeTexture2D<ushort> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return (float)tex2D[(int2)floor(pixelCoord)] / ushort.MaxValue;
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixel(this NativeTexture2D<ushort>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return (float)tex2D[(int2)floor(pixelCoord)] / ushort.MaxValue;
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixel(this NativeTexture2D<byte2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixel(this NativeTexture2D<byte2>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixel(this NativeTexture2D<ushort2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixel(this NativeTexture2D<ushort2>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixel(this NativeTexture2D<byte3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixel(this NativeTexture2D<byte3>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixel(this NativeTexture2D<ushort3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixel(this NativeTexture2D<ushort3>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixel(this NativeTexture2D<byte4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixel(this NativeTexture2D<byte4>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixel(this NativeTexture2D<ushort4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }

    /// <summary>
    /// Reads the pixel value at the specified floating-point coordinate by flooring the coordinate to the nearest integer pixel.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The floating-point coordinate of the pixel.</param>
    /// <returns>The pixel value at the floored coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixel(this NativeTexture2D<ushort4>.ReadOnly tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      return tex2D[(int2)floor(pixelCoord)];
    }
  }
}
