namespace NativeTexture.Extensions
{
  using System.Runtime.CompilerServices;
  using Formats;
  using Unity.Mathematics;

  /// <summary>
  /// Provides extension methods for reading pixel values from NativeTexture2D with bilinear interpolation support.
  /// </summary>
  public static partial class NativeTextureSamplingExtension
  {
    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixelBilinear(this NativeTexture2D<float> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixelBilinear(
      this NativeTexture2D<float>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixelBilinear(this NativeTexture2D<ushort> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadPixelBilinear(
      this NativeTexture2D<ushort>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(this NativeTexture2D<byte2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(
      this NativeTexture2D<byte2>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(this NativeTexture2D<ushort2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(
      this NativeTexture2D<ushort2>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(this NativeTexture2D<byte3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(
      this NativeTexture2D<byte3>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(this NativeTexture2D<ushort3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(
      this NativeTexture2D<ushort3>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(this NativeTexture2D<byte4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(
      this NativeTexture2D<byte4>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(this NativeTexture2D<ushort4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Reads the pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(
      this NativeTexture2D<ushort4>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);

      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);

      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);

      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(this NativeTexture2D<sbyte2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(
      this NativeTexture2D<sbyte2>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(this NativeTexture2D<sbyte3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(
      this NativeTexture2D<sbyte3>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(this NativeTexture2D<sbyte4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(
      this NativeTexture2D<sbyte4>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(this NativeTexture2D<short2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(
      this NativeTexture2D<short2>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(this NativeTexture2D<short3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(
      this NativeTexture2D<short3>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(this NativeTexture2D<short4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(
      this NativeTexture2D<short4>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(this NativeTexture2D<float2> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 ReadPixelBilinear(
      this NativeTexture2D<float2>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x2 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(this NativeTexture2D<float3> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ReadPixelBilinear(
      this NativeTexture2D<float3>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x3 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(this NativeTexture2D<float4> tex2D, float2 pixelCoord)
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 ReadPixelBilinear(
      this NativeTexture2D<float4>.ReadOnly tex2D,
      float2 pixelCoord
    )
    {
      PixelCoord(tex2D, ref pixelCoord);
      PixelFloorCeil(pixelCoord, out int4 pixelFloorCeil, out float2 ratio);
      BilinearSamples(tex2D, ref pixelFloorCeil, out float4x4 samples);
      return BilinearInterpolation(ref samples, ref ratio);
    }

    /// <summary>
    /// Generic method to read pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <typeparam name="T">The type of data stored in the texture.</typeparam>
    /// <param name="tex2D">The NativeTexture2D instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadPixelBilinear<T>(this NativeTexture2D<T> tex2D, float2 pixelCoord)
      where T : unmanaged
    {
      PixelCoord(tex2D, ref pixelCoord);
      return tex2D[(int2)math.floor(pixelCoord)];
    }

    /// <summary>
    /// Generic method to read pixel value at the specified normalized floating-point coordinate using bilinear interpolation.
    /// </summary>
    /// <typeparam name="T">The type of data stored in the texture.</typeparam>
    /// <param name="tex2D">The NativeTexture2D.ReadOnly instance to read from.</param>
    /// <param name="pixelCoord">The normalized floating-point coordinate (range [0,1]) of the pixel.</param>
    /// <returns>The interpolated pixel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadPixelBilinear<T>(this NativeTexture2D<T>.ReadOnly tex2D, float2 pixelCoord)
      where T : unmanaged
    {
      PixelCoord(tex2D, ref pixelCoord);
      return tex2D[(int2)math.floor(pixelCoord)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PixelFloorCeil(float2 pixelCoord, out int4 pixelFloorCeil, out float2 ratio)
    {
      // Calculate floor and ceil coordinates for interpolation
      pixelFloorCeil = new int4((int2)math.floor(pixelCoord), (int2)math.ceil(pixelCoord));

      // Compute interpolation ratios
      ratio = pixelCoord - pixelFloorCeil.xy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PixelCoord<T>(this NativeTexture2D<T> tex2D, ref float2 pixelCoord)
      where T : unmanaged
    {
      // Scale normalized coordinates to texture resolution
      pixelCoord *= tex2D.Resolution;
      pixelCoord = math.clamp(pixelCoord, 0, tex2D.Resolution - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PixelCoord<T>(this NativeTexture2D<T>.ReadOnly tex2D, ref float2 pixelCoord)
      where T : unmanaged
    {
      // Scale normalized coordinates to texture resolution
      pixelCoord *= tex2D.Resolution;
      pixelCoord = math.clamp(pixelCoord, 0, tex2D.Resolution - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float BilinearInterpolation(ref float4 samples, ref float2 ratio)
    {
      // Perform bilinear interpolation
      float f12 = samples.x + ((samples.y - samples.x) * ratio.x);
      float f34 = samples.z + ((samples.w - samples.z) * ratio.x);
      float result = f12 + ((f34 - f12) * ratio.y);

      return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 BilinearInterpolation(ref float4x2 samples, ref float2 ratio) =>
      new(
        BilinearInterpolation(ref samples.c0, ref ratio),
        BilinearInterpolation(ref samples.c1, ref ratio)
      );
  }
}
