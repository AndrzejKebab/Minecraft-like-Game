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
  public struct short4 : IEquatable<short4>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public short x;

    /// <summary>y component of the vector.</summary>
    public short y;

    /// <summary>z component of the vector.</summary>
    public short z;

    /// <summary>w component of the vector.</summary>
    public short w;

    /// <summary>short4 zero value.</summary>
    public static readonly short4 zero = new(0, 0, 0, 0);

    /// <summary>short4 one value (all components are 1).</summary>
    public static readonly short4 one = new(1, 1, 1, 1);

    /// <summary>short4 maximum value (all components are short.MaxValue).</summary>
    public static readonly short4 max = new(
      short.MaxValue,
      short.MaxValue,
      short.MaxValue,
      short.MaxValue
    );

    /// <summary>short4 minimum value (all components are short.MinValue).</summary>
    public static readonly short4 min = new(
      short.MinValue,
      short.MinValue,
      short.MinValue,
      short.MinValue
    );

    /// <summary>Constructs a short4 vector from four short values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short x, short y, short z, short w)
    {
      this.x = x;
      this.y = y;
      this.z = z;
      this.w = w;
    }

    /// <summary>Constructs a short4 vector from a single short value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short v)
    {
      x = v;
      y = v;
      z = v;
      w = v;
    }

    /// <summary>Constructs a short4 vector from a short4 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short4 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
      w = v.w;
    }

    /// <summary>Constructs a short4 vector from a short2 vector and two short values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short2 xy, short z, short w)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
      this.w = w;
    }

    /// <summary>Constructs a short4 vector from a short value, a short2 vector, and a short value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short x, short2 yz, short w)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
      this.w = w;
    }

    /// <summary>Constructs a short4 vector from a short value, and a short3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short x, short3 yzw)
    {
      this.x = x;
      y = yzw.x;
      z = yzw.y;
      w = yzw.z;
    }

    /// <summary>Constructs a short4 vector from two short2 vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(short2 xy, short2 zw)
    {
      x = xy.x;
      y = xy.y;
      z = zw.x;
      w = zw.y;
    }

    /// <summary>Constructs a short4 vector from a float4 vector by truncating the components to the nearest short value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(float4 v)
    {
      x = (short)(v.x / 1f * short.MaxValue);
      y = (short)(v.y / 1f * short.MaxValue);
      z = (short)(v.z / 1f * short.MaxValue);
      w = (short)(v.w / 1f * short.MaxValue);
    }

    /// <summary>Constructs a short4 vector from a sbyte4 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(sbyte4 v)
    {
      x = (short)((float)v.x / sbyte.MaxValue * short.MaxValue);
      y = (short)((float)v.y / sbyte.MaxValue * short.MaxValue);
      z = (short)((float)v.z / sbyte.MaxValue * short.MaxValue);
      w = (short)((float)v.w / sbyte.MaxValue * short.MaxValue);
    }

    /// <summary>Constructs a short4 vector from a single int value by converting it to short and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(int v)
    {
      x = (short)v;
      y = (short)v;
      z = (short)v;
      w = (short)v;
    }

    /// <summary>Constructs a short4 vector from an int4 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short4(int4 v)
    {
      x = (short)v.x;
      y = (short)v.y;
      z = (short)v.z;
      w = (short)v.w;
    }

    /// <summary>
    /// Creates a short4 from a float4 in the -1..1 range, mapping to -32768..32767
    /// Useful for high-precision normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 FromNormalized(float4 v) =>
      new()
      {
        x = (short)(v.x * short.MaxValue),
        y = (short)(v.y * short.MaxValue),
        z = (short)(v.z * short.MaxValue),
        w = (short)(v.w * short.MaxValue),
      };

    /// <summary>
    /// Converts a short4 value (-32768..32767) back to normalized float4 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float4 ToNormalized() =>
      new(
        (float)x / short.MaxValue,
        (float)y / short.MaxValue,
        (float)z / short.MaxValue,
        (float)w / short.MaxValue
      );

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short4(short v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short4(float4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short4(sbyte4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float4(short4 v) => ToFloat4(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float4 ToFloat4(short4 s4) =>
      new(
        (float)s4.x / short.MaxValue,
        (float)s4.y / short.MaxValue,
        (float)s4.z / short.MaxValue,
        (float)s4.w / short.MaxValue
      );

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short4(int4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short4(short3 v) => new(v.x, v.y, v.z, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short2(short4 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator *(short4 lhs, short4 rhs) =>
      new(
        (short)(lhs.x * rhs.x),
        (short)(lhs.y * rhs.y),
        (short)(lhs.z * rhs.z),
        (short)(lhs.w * rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator *(short4 lhs, short rhs) =>
      new((short)(lhs.x * rhs), (short)(lhs.y * rhs), (short)(lhs.z * rhs), (short)(lhs.w * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator *(short lhs, short4 rhs) =>
      new((short)(lhs * rhs.x), (short)(lhs * rhs.y), (short)(lhs * rhs.z), (short)(lhs * rhs.w));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator +(short4 lhs, short4 rhs) =>
      new(
        (short)(lhs.x + rhs.x),
        (short)(lhs.y + rhs.y),
        (short)(lhs.z + rhs.z),
        (short)(lhs.w + rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator +(short4 lhs, short rhs) =>
      new((short)(lhs.x + rhs), (short)(lhs.y + rhs), (short)(lhs.z + rhs), (short)(lhs.w + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator +(short lhs, short4 rhs) =>
      new((short)(lhs + rhs.x), (short)(lhs + rhs.y), (short)(lhs + rhs.z), (short)(lhs + rhs.w));

    // Subtraction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator -(short4 lhs, short4 rhs) =>
      new(
        (short)(lhs.x - rhs.x),
        (short)(lhs.y - rhs.y),
        (short)(lhs.z - rhs.z),
        (short)(lhs.w - rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator -(short4 lhs, short rhs) =>
      new((short)(lhs.x - rhs), (short)(lhs.y - rhs), (short)(lhs.z - rhs), (short)(lhs.w - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator -(short lhs, short4 rhs) =>
      new((short)(lhs - rhs.x), (short)(lhs - rhs.y), (short)(lhs - rhs.z), (short)(lhs - rhs.w));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator /(short4 lhs, short4 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs.x / rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs.y / rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs.z / rhs.z),
        rhs.w == 0 ? (short)0 : (short)(lhs.w / rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator /(short4 lhs, short rhs) =>
      rhs == 0
        ? zero
        : new short4(
          (short)(lhs.x / rhs),
          (short)(lhs.y / rhs),
          (short)(lhs.z / rhs),
          (short)(lhs.w / rhs)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator /(short lhs, short4 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs / rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs / rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs / rhs.z),
        rhs.w == 0 ? (short)0 : (short)(lhs / rhs.w)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator %(short4 lhs, short4 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs.x % rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs.y % rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs.z % rhs.z),
        rhs.w == 0 ? (short)0 : (short)(lhs.w % rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator %(short4 lhs, short rhs) =>
      rhs == 0
        ? zero
        : new short4(
          (short)(lhs.x % rhs),
          (short)(lhs.y % rhs),
          (short)(lhs.z % rhs),
          (short)(lhs.w % rhs)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator %(short lhs, short4 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs % rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs % rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs % rhs.z),
        rhs.w == 0 ? (short)0 : (short)(lhs % rhs.w)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator ++(short4 val) =>
      new((short)(val.x + 1), (short)(val.y + 1), (short)(val.z + 1), (short)(val.w + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator --(short4 val) =>
      new((short)(val.x - 1), (short)(val.y - 1), (short)(val.z - 1), (short)(val.w - 1));

    // Unary negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short4 operator -(short4 val) =>
      new((short)(-val.x), (short)(-val.y), (short)(-val.z), (short)(-val.w));

    // Comparison operators - these return bool4 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(short4 lhs, short4 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z, lhs.w < rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(short4 lhs, short rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs, lhs.w < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(short lhs, short4 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z, lhs < rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(short4 lhs, short4 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z, lhs.w <= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(short4 lhs, short rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs, lhs.w <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(short lhs, short4 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z, lhs <= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(short4 lhs, short4 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z, lhs.w > rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(short4 lhs, short rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs, lhs.w > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(short lhs, short4 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z, lhs > rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(short4 lhs, short4 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z, lhs.w >= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(short4 lhs, short rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs, lhs.w >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(short lhs, short4 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z, lhs >= rhs.w);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(short4 lhs, short4 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z, lhs.w == rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(short4 lhs, short rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs, lhs.w == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(short lhs, short4 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z, lhs == rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(short4 lhs, short4 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z, lhs.w != rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(short4 lhs, short rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs, lhs.w != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(short lhs, short4 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z, lhs != rhs.w);

    // Basic swizzling properties - a small subset of the possible combinations
    public readonly short2 xx => new(x, x);
    public readonly short2 xy => new(x, y);
    public readonly short2 xz => new(x, z);
    public readonly short2 xw => new(x, w);
    public readonly short2 yx => new(y, x);
    public readonly short2 yy => new(y, y);
    public readonly short2 yz => new(y, z);
    public readonly short2 yw => new(y, w);
    public readonly short2 zx => new(z, x);
    public readonly short2 zy => new(z, y);
    public readonly short2 zz => new(z, z);
    public readonly short2 zw => new(z, w);
    public readonly short2 wx => new(w, x);
    public readonly short2 wy => new(w, y);
    public readonly short2 wz => new(w, z);
    public readonly short2 ww => new(w, w);

    // Common short3 swizzles
    public readonly short3 xyz => new(x, y, z);
    public readonly short3 xyw => new(x, y, w);
    public readonly short3 xzy => new(x, z, y);
    public readonly short3 xzw => new(x, z, w);
    public readonly short3 xwy => new(x, w, y);
    public readonly short3 xwz => new(x, w, z);
    public readonly short3 yxz => new(y, x, z);
    public readonly short3 yxw => new(y, x, w);
    public readonly short3 yzx => new(y, z, x);
    public readonly short3 yzw => new(y, z, w);
    public readonly short3 ywx => new(y, w, x);
    public readonly short3 ywz => new(y, w, z);
    public readonly short3 zxy => new(z, x, y);
    public readonly short3 zxw => new(z, x, w);
    public readonly short3 zyx => new(z, y, x);
    public readonly short3 zyw => new(z, y, w);
    public readonly short3 zwx => new(z, w, x);
    public readonly short3 zwy => new(z, w, y);
    public readonly short3 wxy => new(w, x, y);
    public readonly short3 wxz => new(w, x, z);
    public readonly short3 wyx => new(w, y, x);
    public readonly short3 wyz => new(w, y, z);
    public readonly short3 wzx => new(w, z, x);
    public readonly short3 wzy => new(w, z, y);

    // Common 4-component swizzles
    public readonly short4 xxxx => new(x, x, x, x);
    public readonly short4 yyyy => new(y, y, y, y);
    public readonly short4 zzzz => new(z, z, z, z);
    public readonly short4 wwww => new(w, w, w, w);

    // Identity and permutations
    public readonly short4 xyzw => new(x, y, z, w);
    public readonly short4 wzyx => new(w, z, y, x);

    // RGB with different alpha configurations
    public readonly short4 rgbx => new(x, y, z, 0);
    public readonly short4 rgbw => new(x, y, z, w);

    // Color patterns with alpha
    public readonly short4 xyza => new(x, y, z, short.MaxValue);
    public readonly short4 xxxw => new(x, x, x, w);
    public readonly short4 xyzx => new(x, y, z, x);
    public readonly short4 xyzy => new(x, y, z, y);
    public readonly short4 xyzz => new(x, y, z, z);

    /// <summary>Returns the short element at a specified index.</summary>
    public unsafe short this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new ArgumentException("index must be between[0...3]");
#endif
        fixed (short4* array = &this)
        {
          return ((short*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new ArgumentException("index must be between[0...3]");
#endif
        fixed (short* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the short4 is equal to a given short4, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(short4 other) =>
      x == other.x && y == other.y && z == other.z && w == other.w;

    /// <summary>Returns true if the short4 is equal to a given short4, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is short4 other && Equals(other);

    /// <summary>Returns a hash code for the short4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z, w);

    /// <summary>Returns a string representation of the short4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"short4({x}, {y}, {z}, {w})";

    /// <summary>Returns a string representation of the short4 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"short4({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)}, {w.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public short x;
      public short y;
      public short z;
      public short w;

      public DebuggerProxy(short4 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
        w = v.w;
      }
    }
  }
}
