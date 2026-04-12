namespace NativeTexture.Extensions
{
  using System.Runtime.CompilerServices;
  using Formats;
  using Unity.Mathematics;

  public static partial class NativeTextureSamplingExtension
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float> tex2D,
      ref int4 pixelFloorCeil,
      out float4 samples
    ) =>
      // Retrieve pixel values at surrounding coordinates
      samples = math.float4(
        tex2D[pixelFloorCeil.xy],
        tex2D[pixelFloorCeil.xw],
        tex2D[pixelFloorCeil.zy],
        tex2D[pixelFloorCeil.zw]
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4 samples
    ) =>
      // Retrieve pixel values at surrounding coordinates
      samples = math.float4(
        tex2D[pixelFloorCeil.xy],
        tex2D[pixelFloorCeil.xw],
        tex2D[pixelFloorCeil.zy],
        tex2D[pixelFloorCeil.zw]
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort> tex2D,
      ref int4 pixelFloorCeil,
      out float4 samples
    ) =>
      // Retrieve pixel values at surrounding coordinates
      samples = math.float4(
        (float)tex2D[pixelFloorCeil.xy] / ushort.MaxValue,
        (float)tex2D[pixelFloorCeil.xw] / ushort.MaxValue,
        (float)tex2D[pixelFloorCeil.zy] / ushort.MaxValue,
        (float)tex2D[pixelFloorCeil.zw] / ushort.MaxValue
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4 samples
    ) =>
      // Retrieve pixel values at surrounding coordinates
      samples = math.float4(
        (float)tex2D[pixelFloorCeil.xy] / ushort.MaxValue,
        (float)tex2D[pixelFloorCeil.xw] / ushort.MaxValue,
        (float)tex2D[pixelFloorCeil.zy] / ushort.MaxValue,
        (float)tex2D[pixelFloorCeil.zw] / ushort.MaxValue
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte> tex2D,
      ref int4 pixelFloorCeil,
      out float4 samples
    ) =>
      // Retrieve pixel values at surrounding coordinates and normalize to 0..1
      samples = math.float4(
        (float)tex2D[pixelFloorCeil.xy] / byte.MaxValue,
        (float)tex2D[pixelFloorCeil.xw] / byte.MaxValue,
        (float)tex2D[pixelFloorCeil.zy] / byte.MaxValue,
        (float)tex2D[pixelFloorCeil.zw] / byte.MaxValue
      );

    // Optional overload for when byte contains signed -1..1 data normalized to 0..255
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<byte> tex2D,
      ref int4 pixelFloorCeil,
      out float4 samples
    ) =>
      // Retrieve pixel values at surrounding coordinates and normalize to -1..1
      samples = math.float4(
        ((float)tex2D[pixelFloorCeil.xy] / byte.MaxValue * 2f) - 1f,
        ((float)tex2D[pixelFloorCeil.xw] / byte.MaxValue * 2f) - 1f,
        ((float)tex2D[pixelFloorCeil.zy] / byte.MaxValue * 2f) - 1f,
        ((float)tex2D[pixelFloorCeil.zw] / byte.MaxValue * 2f) - 1f
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      byte2 b1 = tex2D[pixelFloorCeil.xy];
      byte2 b2 = tex2D[pixelFloorCeil.xw];
      byte2 b3 = tex2D[pixelFloorCeil.zy];
      byte2 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to float2 using implicit conversion (already normalized to 0..1)
      float2 f1 = b1;
      float2 f2 = b2;
      float2 f3 = b3;
      float2 f4 = b4;

      // Retrieve pixel values at surrounding coordinates
      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte2>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      byte2 b1 = tex2D[pixelFloorCeil.xy];
      byte2 b2 = tex2D[pixelFloorCeil.xw];
      byte2 b3 = tex2D[pixelFloorCeil.zy];
      byte2 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to float2 using implicit conversion (already normalized to 0..1)
      float2 f1 = b1;
      float2 f2 = b2;
      float2 f3 = b3;
      float2 f4 = b4;

      // Retrieve pixel values at surrounding coordinates
      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    // For when byte2 contains signed -1..1 data normalized to 0..255
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<byte2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      byte2 b1 = tex2D[pixelFloorCeil.xy];
      byte2 b2 = tex2D[pixelFloorCeil.xw];
      byte2 b3 = tex2D[pixelFloorCeil.zy];
      byte2 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to normalized -1..1 using the ToNormalized method
      float2 f1 = b1.ToNormalized();
      float2 f2 = b2.ToNormalized();
      float2 f3 = b3.ToNormalized();
      float2 f4 = b4.ToNormalized();

      // Retrieve pixel values at surrounding coordinates
      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      ushort2 u1 = tex2D[pixelFloorCeil.xy];
      ushort2 u2 = tex2D[pixelFloorCeil.xw];
      ushort2 u3 = tex2D[pixelFloorCeil.zy];
      ushort2 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to float2 using implicit conversion (already normalized to 0..1)
      float2 f1 = u1;
      float2 f2 = u2;
      float2 f3 = u3;
      float2 f4 = u4;

      // Retrieve pixel values at surrounding coordinates
      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort2>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      ushort2 u1 = tex2D[pixelFloorCeil.xy];
      ushort2 u2 = tex2D[pixelFloorCeil.xw];
      ushort2 u3 = tex2D[pixelFloorCeil.zy];
      ushort2 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to float2 using implicit conversion (already normalized to 0..1)
      float2 f1 = u1;
      float2 f2 = u2;
      float2 f3 = u3;
      float2 f4 = u4;

      // Retrieve pixel values at surrounding coordinates
      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    // For when ushort2 contains signed -1..1 data normalized to 0..65535
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<ushort2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      ushort2 u1 = tex2D[pixelFloorCeil.xy];
      ushort2 u2 = tex2D[pixelFloorCeil.xw];
      ushort2 u3 = tex2D[pixelFloorCeil.zy];
      ushort2 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to normalized -1..1 using the ToNormalized method
      float2 f1 = u1.ToNormalized();
      float2 f2 = u2.ToNormalized();
      float2 f3 = u3.ToNormalized();
      float2 f4 = u4.ToNormalized();

      // Retrieve pixel values at surrounding coordinates
      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    // For byte3 values
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      byte3 b1 = tex2D[pixelFloorCeil.xy];
      byte3 b2 = tex2D[pixelFloorCeil.xw];
      byte3 b3 = tex2D[pixelFloorCeil.zy];
      byte3 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to float3 using implicit conversion (already normalized to 0..1)
      float3 f1 = b1;
      float3 f2 = b2;
      float3 f3 = b3;
      float3 f4 = b4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte3>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      byte3 b1 = tex2D[pixelFloorCeil.xy];
      byte3 b2 = tex2D[pixelFloorCeil.xw];
      byte3 b3 = tex2D[pixelFloorCeil.zy];
      byte3 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to float3 using implicit conversion (already normalized to 0..1)
      float3 f1 = b1;
      float3 f2 = b2;
      float3 f3 = b3;
      float3 f4 = b4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // For normalized byte3 values (-1..1)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<byte3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      byte3 b1 = tex2D[pixelFloorCeil.xy];
      byte3 b2 = tex2D[pixelFloorCeil.xw];
      byte3 b3 = tex2D[pixelFloorCeil.zy];
      byte3 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to normalized -1..1 using the ToNormalized method
      float3 f1 = b1.ToNormalized();
      float3 f2 = b2.ToNormalized();
      float3 f3 = b3.ToNormalized();
      float3 f4 = b4.ToNormalized();

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // For ushort3 values
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      ushort3 u1 = tex2D[pixelFloorCeil.xy];
      ushort3 u2 = tex2D[pixelFloorCeil.xw];
      ushort3 u3 = tex2D[pixelFloorCeil.zy];
      ushort3 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to float3 using implicit conversion (already normalized to 0..1)
      float3 f1 = u1;
      float3 f2 = u2;
      float3 f3 = u3;
      float3 f4 = u4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort3>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      ushort3 u1 = tex2D[pixelFloorCeil.xy];
      ushort3 u2 = tex2D[pixelFloorCeil.xw];
      ushort3 u3 = tex2D[pixelFloorCeil.zy];
      ushort3 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to float3 using implicit conversion (already normalized to 0..1)
      float3 f1 = u1;
      float3 f2 = u2;
      float3 f3 = u3;
      float3 f4 = u4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // For normalized ushort3 values (-1..1)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<ushort3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      ushort3 u1 = tex2D[pixelFloorCeil.xy];
      ushort3 u2 = tex2D[pixelFloorCeil.xw];
      ushort3 u3 = tex2D[pixelFloorCeil.zy];
      ushort3 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to normalized -1..1 using the ToNormalized method
      float3 f1 = u1.ToNormalized();
      float3 f2 = u2.ToNormalized();
      float3 f3 = u3.ToNormalized();
      float3 f4 = u4.ToNormalized();

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // Bilinear interpolation for float4x3 (used with byte3 and ushort3)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float3 BilinearInterpolation(ref float4x3 samples, ref float2 ratio)
    {
      float3 f12 = new(
        samples.c0.x + ((samples.c0.y - samples.c0.x) * ratio.x),
        samples.c1.x + ((samples.c1.y - samples.c1.x) * ratio.x),
        samples.c2.x + ((samples.c2.y - samples.c2.x) * ratio.x)
      );

      float3 f34 = new(
        samples.c0.z + ((samples.c0.w - samples.c0.z) * ratio.x),
        samples.c1.z + ((samples.c1.w - samples.c1.z) * ratio.x),
        samples.c2.z + ((samples.c2.w - samples.c2.z) * ratio.x)
      );

      return f12 + ((f34 - f12) * ratio.y);
    }

    // For byte4 values
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      byte4 b1 = tex2D[pixelFloorCeil.xy];
      byte4 b2 = tex2D[pixelFloorCeil.xw];
      byte4 b3 = tex2D[pixelFloorCeil.zy];
      byte4 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to float4 using implicit conversion (already normalized to 0..1)
      float4 f1 = b1;
      float4 f2 = b2;
      float4 f3 = b3;
      float4 f4 = b4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x4(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z,
        f1.w,
        f2.w,
        f3.w,
        f4.w
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<byte4>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      byte4 b1 = tex2D[pixelFloorCeil.xy];
      byte4 b2 = tex2D[pixelFloorCeil.xw];
      byte4 b3 = tex2D[pixelFloorCeil.zy];
      byte4 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to float4 using implicit conversion (already normalized to 0..1)
      float4 f1 = b1;
      float4 f2 = b2;
      float4 f3 = b3;
      float4 f4 = b4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x4(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z,
        f1.w,
        f2.w,
        f3.w,
        f4.w
      );
    }

    // For normalized byte4 values (-1..1)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<byte4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      byte4 b1 = tex2D[pixelFloorCeil.xy];
      byte4 b2 = tex2D[pixelFloorCeil.xw];
      byte4 b3 = tex2D[pixelFloorCeil.zy];
      byte4 b4 = tex2D[pixelFloorCeil.zw];

      // Convert to normalized -1..1 using the ToNormalized method
      float4 f1 = b1.ToNormalized();
      float4 f2 = b2.ToNormalized();
      float4 f3 = b3.ToNormalized();
      float4 f4 = b4.ToNormalized();

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x4(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z,
        f1.w,
        f2.w,
        f3.w,
        f4.w
      );
    }

    // For ushort4 values
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      ushort4 u1 = tex2D[pixelFloorCeil.xy];
      ushort4 u2 = tex2D[pixelFloorCeil.xw];
      ushort4 u3 = tex2D[pixelFloorCeil.zy];
      ushort4 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to float4 using implicit conversion (already normalized to 0..1)
      float4 f1 = u1;
      float4 f2 = u2;
      float4 f3 = u3;
      float4 f4 = u4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x4(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z,
        f1.w,
        f2.w,
        f3.w,
        f4.w
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<ushort4>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      ushort4 u1 = tex2D[pixelFloorCeil.xy];
      ushort4 u2 = tex2D[pixelFloorCeil.xw];
      ushort4 u3 = tex2D[pixelFloorCeil.zy];
      ushort4 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to float4 using implicit conversion (already normalized to 0..1)
      float4 f1 = u1;
      float4 f2 = u2;
      float4 f3 = u3;
      float4 f4 = u4;

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x4(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z,
        f1.w,
        f2.w,
        f3.w,
        f4.w
      );
    }

    // For normalized ushort4 values (-1..1)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamplesNormalized(
      this NativeTexture2D<ushort4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      ushort4 u1 = tex2D[pixelFloorCeil.xy];
      ushort4 u2 = tex2D[pixelFloorCeil.xw];
      ushort4 u3 = tex2D[pixelFloorCeil.zy];
      ushort4 u4 = tex2D[pixelFloorCeil.zw];

      // Convert to normalized -1..1 using the ToNormalized method
      float4 f1 = u1.ToNormalized();
      float4 f2 = u2.ToNormalized();
      float4 f3 = u3.ToNormalized();
      float4 f4 = u4.ToNormalized();

      // Retrieve pixel values at surrounding coordinates
      samples = new float4x4(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z,
        f1.w,
        f2.w,
        f3.w,
        f4.w
      );
    }

    // sbyte2
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<sbyte2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      sbyte2 s1 = tex2D[pixelFloorCeil.xy];
      sbyte2 s2 = tex2D[pixelFloorCeil.xw];
      sbyte2 s3 = tex2D[pixelFloorCeil.zy];
      sbyte2 s4 = tex2D[pixelFloorCeil.zw];

      float2 f1 = s1;
      float2 f2 = s2;
      float2 f3 = s3;
      float2 f4 = s4;

      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<sbyte2>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      sbyte2 s1 = tex2D[pixelFloorCeil.xy];
      sbyte2 s2 = tex2D[pixelFloorCeil.xw];
      sbyte2 s3 = tex2D[pixelFloorCeil.zy];
      sbyte2 s4 = tex2D[pixelFloorCeil.zw];

      float2 f1 = s1;
      float2 f2 = s2;
      float2 f3 = s3;
      float2 f4 = s4;

      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    // short2
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<short2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      short2 s1 = tex2D[pixelFloorCeil.xy];
      short2 s2 = tex2D[pixelFloorCeil.xw];
      short2 s3 = tex2D[pixelFloorCeil.zy];
      short2 s4 = tex2D[pixelFloorCeil.zw];

      float2 f1 = s1;
      float2 f2 = s2;
      float2 f3 = s3;
      float2 f4 = s4;

      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<short2>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      short2 s1 = tex2D[pixelFloorCeil.xy];
      short2 s2 = tex2D[pixelFloorCeil.xw];
      short2 s3 = tex2D[pixelFloorCeil.zy];
      short2 s4 = tex2D[pixelFloorCeil.zw];

      float2 f1 = s1;
      float2 f2 = s2;
      float2 f3 = s3;
      float2 f4 = s4;

      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    // float2
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float2> tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      float2 f1 = tex2D[pixelFloorCeil.xy];
      float2 f2 = tex2D[pixelFloorCeil.xw];
      float2 f3 = tex2D[pixelFloorCeil.zy];
      float2 f4 = tex2D[pixelFloorCeil.zw];

      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float2>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x2 samples
    )
    {
      float2 f1 = tex2D[pixelFloorCeil.xy];
      float2 f2 = tex2D[pixelFloorCeil.xw];
      float2 f3 = tex2D[pixelFloorCeil.zy];
      float2 f4 = tex2D[pixelFloorCeil.zw];

      samples = math.float4x2(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y)
      );
    }

    // sbyte3
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<sbyte3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      sbyte3 s1 = tex2D[pixelFloorCeil.xy];
      sbyte3 s2 = tex2D[pixelFloorCeil.xw];
      sbyte3 s3 = tex2D[pixelFloorCeil.zy];
      sbyte3 s4 = tex2D[pixelFloorCeil.zw];

      float3 f1 = s1;
      float3 f2 = s2;
      float3 f3 = s3;
      float3 f4 = s4;

      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<sbyte3>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      sbyte3 s1 = tex2D[pixelFloorCeil.xy];
      sbyte3 s2 = tex2D[pixelFloorCeil.xw];
      sbyte3 s3 = tex2D[pixelFloorCeil.zy];
      sbyte3 s4 = tex2D[pixelFloorCeil.zw];

      float3 f1 = s1;
      float3 f2 = s2;
      float3 f3 = s3;
      float3 f4 = s4;

      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // short3
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<short3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      short3 s1 = tex2D[pixelFloorCeil.xy];
      short3 s2 = tex2D[pixelFloorCeil.xw];
      short3 s3 = tex2D[pixelFloorCeil.zy];
      short3 s4 = tex2D[pixelFloorCeil.zw];

      float3 f1 = s1;
      float3 f2 = s2;
      float3 f3 = s3;
      float3 f4 = s4;

      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<short3>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      short3 s1 = tex2D[pixelFloorCeil.xy];
      short3 s2 = tex2D[pixelFloorCeil.xw];
      short3 s3 = tex2D[pixelFloorCeil.zy];
      short3 s4 = tex2D[pixelFloorCeil.zw];

      float3 f1 = s1;
      float3 f2 = s2;
      float3 f3 = s3;
      float3 f4 = s4;

      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // float3
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float3> tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      float3 f1 = tex2D[pixelFloorCeil.xy];
      float3 f2 = tex2D[pixelFloorCeil.xw];
      float3 f3 = tex2D[pixelFloorCeil.zy];
      float3 f4 = tex2D[pixelFloorCeil.zw];

      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float3>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x3 samples
    )
    {
      float3 f1 = tex2D[pixelFloorCeil.xy];
      float3 f2 = tex2D[pixelFloorCeil.xw];
      float3 f3 = tex2D[pixelFloorCeil.zy];
      float3 f4 = tex2D[pixelFloorCeil.zw];

      samples = new float4x3(
        f1.x,
        f2.x,
        f3.x,
        f4.x,
        f1.y,
        f2.y,
        f3.y,
        f4.y,
        f1.z,
        f2.z,
        f3.z,
        f4.z
      );
    }

    // sbyte4
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<sbyte4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      sbyte4 s1 = tex2D[pixelFloorCeil.xy];
      sbyte4 s2 = tex2D[pixelFloorCeil.xw];
      sbyte4 s3 = tex2D[pixelFloorCeil.zy];
      sbyte4 s4 = tex2D[pixelFloorCeil.zw];

      float4 f1 = s1;
      float4 f2 = s2;
      float4 f3 = s3;
      float4 f4 = s4;

      samples = new float4x4(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y),
        math.float4(f1.z, f2.z, f3.z, f4.z),
        math.float4(f1.w, f2.w, f3.w, f4.w)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<sbyte4>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      sbyte4 s1 = tex2D[pixelFloorCeil.xy];
      sbyte4 s2 = tex2D[pixelFloorCeil.xw];
      sbyte4 s3 = tex2D[pixelFloorCeil.zy];
      sbyte4 s4 = tex2D[pixelFloorCeil.zw];

      float4 f1 = s1;
      float4 f2 = s2;
      float4 f3 = s3;
      float4 f4 = s4;

      samples = new float4x4(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y),
        math.float4(f1.z, f2.z, f3.z, f4.z),
        math.float4(f1.w, f2.w, f3.w, f4.w)
      );
    }

    // short4
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<short4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      short4 s1 = tex2D[pixelFloorCeil.xy];
      short4 s2 = tex2D[pixelFloorCeil.xw];
      short4 s3 = tex2D[pixelFloorCeil.zy];
      short4 s4 = tex2D[pixelFloorCeil.zw];

      float4 f1 = s1;
      float4 f2 = s2;
      float4 f3 = s3;
      float4 f4 = s4;

      samples = new float4x4(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y),
        math.float4(f1.z, f2.z, f3.z, f4.z),
        math.float4(f1.w, f2.w, f3.w, f4.w)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<short4>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      short4 s1 = tex2D[pixelFloorCeil.xy];
      short4 s2 = tex2D[pixelFloorCeil.xw];
      short4 s3 = tex2D[pixelFloorCeil.zy];
      short4 s4 = tex2D[pixelFloorCeil.zw];

      float4 f1 = s1;
      float4 f2 = s2;
      float4 f3 = s3;
      float4 f4 = s4;

      samples = new float4x4(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y),
        math.float4(f1.z, f2.z, f3.z, f4.z),
        math.float4(f1.w, f2.w, f3.w, f4.w)
      );
    }

    // float4
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float4> tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      float4 f1 = tex2D[pixelFloorCeil.xy];
      float4 f2 = tex2D[pixelFloorCeil.xw];
      float4 f3 = tex2D[pixelFloorCeil.zy];
      float4 f4 = tex2D[pixelFloorCeil.zw];

      samples = new float4x4(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y),
        math.float4(f1.z, f2.z, f3.z, f4.z),
        math.float4(f1.w, f2.w, f3.w, f4.w)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BilinearSamples(
      this NativeTexture2D<float4>.ReadOnly tex2D,
      ref int4 pixelFloorCeil,
      out float4x4 samples
    )
    {
      float4 f1 = tex2D[pixelFloorCeil.xy];
      float4 f2 = tex2D[pixelFloorCeil.xw];
      float4 f3 = tex2D[pixelFloorCeil.zy];
      float4 f4 = tex2D[pixelFloorCeil.zw];

      samples = new float4x4(
        math.float4(f1.x, f2.x, f3.x, f4.x),
        math.float4(f1.y, f2.y, f3.y, f4.y),
        math.float4(f1.z, f2.z, f3.z, f4.z),
        math.float4(f1.w, f2.w, f3.w, f4.w)
      );
    }

    // Bilinear interpolation for float4x4 (used with byte4 and ushort4)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float4 BilinearInterpolation(ref float4x4 samples, ref float2 ratio)
    {
      float4 f12 = new(
        samples.c0.x + ((samples.c0.y - samples.c0.x) * ratio.x),
        samples.c1.x + ((samples.c1.y - samples.c1.x) * ratio.x),
        samples.c2.x + ((samples.c2.y - samples.c2.x) * ratio.x),
        samples.c3.x + ((samples.c3.y - samples.c3.x) * ratio.x)
      );

      float4 f34 = new(
        samples.c0.z + ((samples.c0.w - samples.c0.z) * ratio.x),
        samples.c1.z + ((samples.c1.w - samples.c1.z) * ratio.x),
        samples.c2.z + ((samples.c2.w - samples.c2.z) * ratio.x),
        samples.c3.z + ((samples.c3.w - samples.c3.z) * ratio.x)
      );

      return f12 + ((f34 - f12) * ratio.y);
    }
  }
}
