// ReSharper disable InconsistentNaming

namespace NativeTexture.Formats
{
  using System;
  using System.Diagnostics;
  using System.Runtime.CompilerServices;
  using System.Runtime.InteropServices;
  using Unity.Mathematics;

  // ReSharper disable once InconsistentNaming
  [Serializable]
  [StructLayout(LayoutKind.Sequential)]
  [DebuggerTypeProxy(typeof(DebuggerProxy))]
#pragma warning disable IDE1006 // Naming Styles
  public struct byte4 : IEquatable<byte4>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public byte x;

    /// <summary>y component of the vector.</summary>
    public byte y;

    /// <summary>z component of the vector.</summary>
    public byte z;

    /// <summary>w component of the vector.</summary>
    public byte w;

    /// <summary>byte4 zero value.</summary>
    public static readonly byte4 zero = new(0, 0, 0, 0);

    /// <summary>byte4 one value (all components are 1).</summary>
    public static readonly byte4 one = new(1, 1, 1, 1);

    /// <summary>byte4 maximum value (all components are byte.MaxValue).</summary>
    public static readonly byte4 max = new(
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue
    );

    /// <summary>Constructs a byte4 vector from four byte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte x, byte y, byte z, byte w)
    {
      this.x = x;
      this.y = y;
      this.z = z;
      this.w = w;
    }

    /// <summary>Constructs a byte4 vector from a single byte value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte v)
    {
      x = v;
      y = v;
      z = v;
      w = v;
    }

    /// <summary>Constructs a byte4 vector from a byte4 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte4 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
      w = v.w;
    }

    /// <summary>Constructs a byte4 vector from a byte2 vector and two byte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte2 xy, byte z, byte w)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
      this.w = w;
    }

    /// <summary>Constructs a byte4 vector from a byte value, a byte2 vector, and a byte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte x, byte2 yz, byte w)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
      this.w = w;
    }

    /// <summary>Constructs a byte4 vector from a byte value, and a byte3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte x, byte3 yzw)
    {
      this.x = x;
      y = yzw.x;
      z = yzw.y;
      w = yzw.z;
    }

    /// <summary>Constructs a byte4 vector from two byte2 vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(byte2 xy, byte2 zw)
    {
      x = xy.x;
      y = xy.y;
      z = zw.x;
      w = zw.y;
    }

    /// <summary>Constructs a byte4 vector from a float4 vector by truncating the components to the nearest byte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(float4 v)
    {
      x = (byte)(v.x / 1f * byte.MaxValue);
      y = (byte)(v.y / 1f * byte.MaxValue);
      z = (byte)(v.z / 1f * byte.MaxValue);
      w = (byte)(v.w / 1f * byte.MaxValue);
    }

    /// <summary>Constructs a byte4 vector from a ushort4 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(ushort4 v)
    {
      x = (byte)((float)v.x / ushort.MaxValue * byte.MaxValue);
      y = (byte)((float)v.y / ushort.MaxValue * byte.MaxValue);
      z = (byte)((float)v.z / ushort.MaxValue * byte.MaxValue);
      w = (byte)((float)v.w / ushort.MaxValue * byte.MaxValue);
    }

    /// <summary>Constructs a byte4 vector from a single int value by converting it to byte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(int v)
    {
      x = (byte)v;
      y = (byte)v;
      z = (byte)v;
      w = (byte)v;
    }

    /// <summary>Constructs a byte4 vector from an int4 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(int4 v)
    {
      x = (byte)v.x;
      y = (byte)v.y;
      z = (byte)v.z;
      w = (byte)v.w;
    }

    /// <summary>Constructs a byte4 vector from a single uint value by converting it to byte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(uint v)
    {
      x = (byte)v;
      y = (byte)v;
      z = (byte)v;
      w = (byte)v;
    }

    /// <summary>Constructs a byte4 vector from a uint4 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte4(uint4 v)
    {
      x = (byte)v.x;
      y = (byte)v.y;
      z = (byte)v.z;
      w = (byte)v.w;
    }

    /// <summary>
    /// Creates a byte4 from a float4 in the -1..1 range, mapping to 0..255
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 FromNormalized(float4 v) =>
      new()
      {
        x = (byte)(((v.x * 0.5f) + 0.5f) * byte.MaxValue),
        y = (byte)(((v.y * 0.5f) + 0.5f) * byte.MaxValue),
        z = (byte)(((v.z * 0.5f) + 0.5f) * byte.MaxValue),
        w = (byte)(((v.w * 0.5f) + 0.5f) * byte.MaxValue),
      };

    /// <summary>
    /// Converts a byte4 value (0..255) back to normalized float4 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float4 ToNormalized() =>
      new(
        ((float)x / byte.MaxValue * 2f) - 1f,
        ((float)y / byte.MaxValue * 2f) - 1f,
        ((float)z / byte.MaxValue * 2f) - 1f,
        ((float)w / byte.MaxValue * 2f) - 1f
      );

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte4(byte v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte4(float4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte4(ushort4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float4(byte4 v) => ToFloat4(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float4 ToFloat4(byte4 b4) =>
      new(
        (float)b4.x / byte.MaxValue,
        (float)b4.y / byte.MaxValue,
        (float)b4.z / byte.MaxValue,
        (float)b4.w / byte.MaxValue
      );

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte4(int4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte4(uint4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte4(byte3 v) => new(v.x, v.y, v.z, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte2(byte4 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator *(byte4 lhs, byte4 rhs) =>
      new(
        (byte)(lhs.x * rhs.x),
        (byte)(lhs.y * rhs.y),
        (byte)(lhs.z * rhs.z),
        (byte)(lhs.w * rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator *(byte4 lhs, byte rhs) =>
      new((byte)(lhs.x * rhs), (byte)(lhs.y * rhs), (byte)(lhs.z * rhs), (byte)(lhs.w * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator *(byte lhs, byte4 rhs) =>
      new((byte)(lhs * rhs.x), (byte)(lhs * rhs.y), (byte)(lhs * rhs.z), (byte)(lhs * rhs.w));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator +(byte4 lhs, byte4 rhs) =>
      new(
        (byte)(lhs.x + rhs.x),
        (byte)(lhs.y + rhs.y),
        (byte)(lhs.z + rhs.z),
        (byte)(lhs.w + rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator +(byte4 lhs, byte rhs) =>
      new((byte)(lhs.x + rhs), (byte)(lhs.y + rhs), (byte)(lhs.z + rhs), (byte)(lhs.w + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator +(byte lhs, byte4 rhs) =>
      new((byte)(lhs + rhs.x), (byte)(lhs + rhs.y), (byte)(lhs + rhs.z), (byte)(lhs + rhs.w));

    // Subtraction (with clamping to zero)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator -(byte4 lhs, byte4 rhs) =>
      new(
        (byte)Math.Max(0, lhs.x - rhs.x),
        (byte)Math.Max(0, lhs.y - rhs.y),
        (byte)Math.Max(0, lhs.z - rhs.z),
        (byte)Math.Max(0, lhs.w - rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator -(byte4 lhs, byte rhs) =>
      new(
        (byte)Math.Max(0, lhs.x - rhs),
        (byte)Math.Max(0, lhs.y - rhs),
        (byte)Math.Max(0, lhs.z - rhs),
        (byte)Math.Max(0, lhs.w - rhs)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator -(byte lhs, byte4 rhs) =>
      new(
        (byte)Math.Max(0, lhs - rhs.x),
        (byte)Math.Max(0, lhs - rhs.y),
        (byte)Math.Max(0, lhs - rhs.z),
        (byte)Math.Max(0, lhs - rhs.w)
      );

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator /(byte4 lhs, byte4 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs.x / rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs.y / rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs.z / rhs.z),
        rhs.w == 0 ? (byte)0 : (byte)(lhs.w / rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator /(byte4 lhs, byte rhs) =>
      rhs == 0
        ? zero
        : new byte4(
          (byte)(lhs.x / rhs),
          (byte)(lhs.y / rhs),
          (byte)(lhs.z / rhs),
          (byte)(lhs.w / rhs)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator /(byte lhs, byte4 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs / rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs / rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs / rhs.z),
        rhs.w == 0 ? (byte)0 : (byte)(lhs / rhs.w)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator %(byte4 lhs, byte4 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs.x % rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs.y % rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs.z % rhs.z),
        rhs.w == 0 ? (byte)0 : (byte)(lhs.w % rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator %(byte4 lhs, byte rhs) =>
      rhs == 0
        ? zero
        : new byte4(
          (byte)(lhs.x % rhs),
          (byte)(lhs.y % rhs),
          (byte)(lhs.z % rhs),
          (byte)(lhs.w % rhs)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator %(byte lhs, byte4 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs % rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs % rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs % rhs.z),
        rhs.w == 0 ? (byte)0 : (byte)(lhs % rhs.w)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator ++(byte4 val) =>
      new((byte)(val.x + 1), (byte)(val.y + 1), (byte)(val.z + 1), (byte)(val.w + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte4 operator --(byte4 val) =>
      new(
        (byte)Math.Max(0, val.x - 1),
        (byte)Math.Max(0, val.y - 1),
        (byte)Math.Max(0, val.z - 1),
        (byte)Math.Max(0, val.w - 1)
      );

    // Comparison operators - these return bool4 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(byte4 lhs, byte4 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z, lhs.w < rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(byte4 lhs, byte rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs, lhs.w < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(byte lhs, byte4 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z, lhs < rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(byte4 lhs, byte4 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z, lhs.w <= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(byte4 lhs, byte rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs, lhs.w <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(byte lhs, byte4 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z, lhs <= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(byte4 lhs, byte4 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z, lhs.w > rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(byte4 lhs, byte rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs, lhs.w > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(byte lhs, byte4 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z, lhs > rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(byte4 lhs, byte4 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z, lhs.w >= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(byte4 lhs, byte rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs, lhs.w >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(byte lhs, byte4 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z, lhs >= rhs.w);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(byte4 lhs, byte4 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z, lhs.w == rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(byte4 lhs, byte rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs, lhs.w == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(byte lhs, byte4 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z, lhs == rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(byte4 lhs, byte4 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z, lhs.w != rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(byte4 lhs, byte rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs, lhs.w != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(byte lhs, byte4 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z, lhs != rhs.w);

    // Basic swizzling properties - a small subset of the possible combinations
    public readonly byte2 xx => new(x, x);
    public readonly byte2 xy => new(x, y);
    public readonly byte2 xz => new(x, z);
    public readonly byte2 xw => new(x, w);
    public readonly byte2 yx => new(y, x);
    public readonly byte2 yy => new(y, y);
    public readonly byte2 yz => new(y, z);
    public readonly byte2 yw => new(y, w);
    public readonly byte2 zx => new(z, x);
    public readonly byte2 zy => new(z, y);
    public readonly byte2 zz => new(z, z);
    public readonly byte2 zw => new(z, w);
    public readonly byte2 wx => new(w, x);
    public readonly byte2 wy => new(w, y);
    public readonly byte2 wz => new(w, z);
    public readonly byte2 ww => new(w, w);

    // Common byte3 swizzles
    public readonly byte3 xyz => new(x, y, z);
    public readonly byte3 xyw => new(x, y, w);
    public readonly byte3 xzy => new(x, z, y);
    public readonly byte3 xzw => new(x, z, w);
    public readonly byte3 xwy => new(x, w, y);
    public readonly byte3 xwz => new(x, w, z);
    public readonly byte3 yxz => new(y, x, z);
    public readonly byte3 yxw => new(y, x, w);
    public readonly byte3 yzx => new(y, z, x);
    public readonly byte3 yzw => new(y, z, w);
    public readonly byte3 ywx => new(y, w, x);
    public readonly byte3 ywz => new(y, w, z);
    public readonly byte3 zxy => new(z, x, y);
    public readonly byte3 zxw => new(z, x, w);
    public readonly byte3 zyx => new(z, y, x);
    public readonly byte3 zyw => new(z, y, w);
    public readonly byte3 zwx => new(z, w, x);
    public readonly byte3 zwy => new(z, w, y);
    public readonly byte3 wxy => new(w, x, y);
    public readonly byte3 wxz => new(w, x, z);
    public readonly byte3 wyx => new(w, y, x);
    public readonly byte3 wyz => new(w, y, z);
    public readonly byte3 wzx => new(w, z, x);
    public readonly byte3 wzy => new(w, z, y);

    // Common 4-component swizzles
    public readonly byte4 xxxx => new(x, x, x, x);
    public readonly byte4 yyyy => new(y, y, y, y);
    public readonly byte4 zzzz => new(z, z, z, z);
    public readonly byte4 wwww => new(w, w, w, w);

    // Identity and permutations
    public readonly byte4 xyzw => new(x, y, z, w);
    public readonly byte4 wzyx => new(w, z, y, x);

    // RGB with different alpha configurations
    public readonly byte4 rgbx => new(x, y, z, 0);
    public readonly byte4 rgbw => new(x, y, z, w);

    // Color patterns with alpha
    public readonly byte4 xyza => new(x, y, z, byte.MaxValue);
    public readonly byte4 xxxw => new(x, x, x, w);
    public readonly byte4 xyzx => new(x, y, z, x);
    public readonly byte4 xyzy => new(x, y, z, y);
    public readonly byte4 xyzz => new(x, y, z, z);

    /// <summary>Returns the byte element at a specified index.</summary>
    public unsafe byte this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new ArgumentException("index must be between[0...3]");
#endif
        fixed (byte4* array = &this)
        {
          return ((byte*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new ArgumentException("index must be between[0...3]");
#endif
        fixed (byte* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the byte4 is equal to a given byte4, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(byte4 other) =>
      x == other.x && y == other.y && z == other.z && w == other.w;

    /// <summary>Returns true if the byte4 is equal to a given byte4, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is byte4 other && Equals(other);

    /// <summary>Returns a hash code for the byte4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z, w);

    /// <summary>Returns a string representation of the byte4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"byte4({x}, {y}, {z}, {w})";

    /// <summary>Returns a string representation of the byte4 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"byte4({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)}, {w.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public byte x;
      public byte y;
      public byte z;
      public byte w;

      public DebuggerProxy(byte4 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
        w = v.w;
      }
    }
  }
}
