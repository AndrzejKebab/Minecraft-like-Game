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
  public struct sbyte4 : IEquatable<sbyte4>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public sbyte x;

    /// <summary>y component of the vector.</summary>
    public sbyte y;

    /// <summary>z component of the vector.</summary>
    public sbyte z;

    /// <summary>w component of the vector.</summary>
    public sbyte w;

    /// <summary>sbyte4 zero value.</summary>
    public static readonly sbyte4 zero = new(0, 0, 0, 0);

    /// <summary>sbyte4 one value (all components are 1).</summary>
    public static readonly sbyte4 one = new(1, 1, 1, 1);

    /// <summary>sbyte4 maximum value (all components are sbyte.MaxValue).</summary>
    public static readonly sbyte4 max = new(
      sbyte.MaxValue,
      sbyte.MaxValue,
      sbyte.MaxValue,
      sbyte.MaxValue
    );

    /// <summary>sbyte4 minimum value (all components are sbyte.MinValue).</summary>
    public static readonly sbyte4 min = new(
      sbyte.MinValue,
      sbyte.MinValue,
      sbyte.MinValue,
      sbyte.MinValue
    );

    /// <summary>Constructs a sbyte4 vector from four sbyte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte x, sbyte y, sbyte z, sbyte w)
    {
      this.x = x;
      this.y = y;
      this.z = z;
      this.w = w;
    }

    /// <summary>Constructs a sbyte4 vector from a single sbyte value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte v)
    {
      x = v;
      y = v;
      z = v;
      w = v;
    }

    /// <summary>Constructs a sbyte4 vector from a sbyte4 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte4 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
      w = v.w;
    }

    /// <summary>Constructs a sbyte4 vector from a sbyte2 vector and two sbyte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte2 xy, sbyte z, sbyte w)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
      this.w = w;
    }

    /// <summary>Constructs a sbyte4 vector from a sbyte value, a sbyte2 vector, and a sbyte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte x, sbyte2 yz, sbyte w)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
      this.w = w;
    }

    /// <summary>Constructs a sbyte4 vector from a sbyte value, and a sbyte3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte x, sbyte3 yzw)
    {
      this.x = x;
      y = yzw.x;
      z = yzw.y;
      w = yzw.z;
    }

    /// <summary>Constructs a sbyte4 vector from two sbyte2 vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte2 xy, sbyte2 zw)
    {
      x = xy.x;
      y = xy.y;
      z = zw.x;
      w = zw.y;
    }

    /// <summary>Constructs a sbyte4 vector from a float4 vector by truncating the components to the nearest sbyte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(float4 v)
    {
      x = (sbyte)(v.x / 1f * sbyte.MaxValue);
      y = (sbyte)(v.y / 1f * sbyte.MaxValue);
      z = (sbyte)(v.z / 1f * sbyte.MaxValue);
      w = (sbyte)(v.w / 1f * sbyte.MaxValue);
    }

    /// <summary>Constructs a sbyte4 vector from a short4 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(short4 v)
    {
      x = (sbyte)((float)v.x / short.MaxValue * sbyte.MaxValue);
      y = (sbyte)((float)v.y / short.MaxValue * sbyte.MaxValue);
      z = (sbyte)((float)v.z / short.MaxValue * sbyte.MaxValue);
      w = (sbyte)((float)v.w / short.MaxValue * sbyte.MaxValue);
    }

    /// <summary>Constructs a sbyte4 vector from a single int value by converting it to sbyte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(int v)
    {
      x = (sbyte)v;
      y = (sbyte)v;
      z = (sbyte)v;
      w = (sbyte)v;
    }

    /// <summary>Constructs a sbyte4 vector from an int4 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(int4 v)
    {
      x = (sbyte)v.x;
      y = (sbyte)v.y;
      z = (sbyte)v.z;
      w = (sbyte)v.w;
    }

    /// <summary>
    /// Creates a sbyte4 from a float4 in the -1..1 range, mapping to -128..127
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 FromNormalized(float4 v) =>
      new()
      {
        x = (sbyte)(v.x * sbyte.MaxValue),
        y = (sbyte)(v.y * sbyte.MaxValue),
        z = (sbyte)(v.z * sbyte.MaxValue),
        w = (sbyte)(v.w * sbyte.MaxValue),
      };

    /// <summary>
    /// Converts a sbyte4 value (-128..127) back to normalized float4 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float4 ToNormalized() =>
      new(
        (float)x / sbyte.MaxValue,
        (float)y / sbyte.MaxValue,
        (float)z / sbyte.MaxValue,
        (float)w / sbyte.MaxValue
      );

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte4(sbyte v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte4(float4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte4(short4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float4(sbyte4 v) => ToFloat4(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float4 ToFloat4(sbyte4 s4) =>
      new(
        (float)s4.x / sbyte.MaxValue,
        (float)s4.y / sbyte.MaxValue,
        (float)s4.z / sbyte.MaxValue,
        (float)s4.w / sbyte.MaxValue
      );

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte4(int4 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte4(sbyte3 v) => new(v.x, v.y, v.z, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte2(sbyte4 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator *(sbyte4 lhs, sbyte4 rhs) =>
      new(
        (sbyte)(lhs.x * rhs.x),
        (sbyte)(lhs.y * rhs.y),
        (sbyte)(lhs.z * rhs.z),
        (sbyte)(lhs.w * rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator *(sbyte4 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x * rhs), (sbyte)(lhs.y * rhs), (sbyte)(lhs.z * rhs), (sbyte)(lhs.w * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator *(sbyte lhs, sbyte4 rhs) =>
      new((sbyte)(lhs * rhs.x), (sbyte)(lhs * rhs.y), (sbyte)(lhs * rhs.z), (sbyte)(lhs * rhs.w));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator +(sbyte4 lhs, sbyte4 rhs) =>
      new(
        (sbyte)(lhs.x + rhs.x),
        (sbyte)(lhs.y + rhs.y),
        (sbyte)(lhs.z + rhs.z),
        (sbyte)(lhs.w + rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator +(sbyte4 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x + rhs), (sbyte)(lhs.y + rhs), (sbyte)(lhs.z + rhs), (sbyte)(lhs.w + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator +(sbyte lhs, sbyte4 rhs) =>
      new((sbyte)(lhs + rhs.x), (sbyte)(lhs + rhs.y), (sbyte)(lhs + rhs.z), (sbyte)(lhs + rhs.w));

    // Subtraction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator -(sbyte4 lhs, sbyte4 rhs) =>
      new(
        (sbyte)(lhs.x - rhs.x),
        (sbyte)(lhs.y - rhs.y),
        (sbyte)(lhs.z - rhs.z),
        (sbyte)(lhs.w - rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator -(sbyte4 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x - rhs), (sbyte)(lhs.y - rhs), (sbyte)(lhs.z - rhs), (sbyte)(lhs.w - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator -(sbyte lhs, sbyte4 rhs) =>
      new((sbyte)(lhs - rhs.x), (sbyte)(lhs - rhs.y), (sbyte)(lhs - rhs.z), (sbyte)(lhs - rhs.w));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator /(sbyte4 lhs, sbyte4 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs.x / rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs.y / rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs.z / rhs.z),
        rhs.w == 0 ? (sbyte)0 : (sbyte)(lhs.w / rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator /(sbyte4 lhs, sbyte rhs) =>
      rhs == 0
        ? zero
        : new sbyte4(
          (sbyte)(lhs.x / rhs),
          (sbyte)(lhs.y / rhs),
          (sbyte)(lhs.z / rhs),
          (sbyte)(lhs.w / rhs)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator /(sbyte lhs, sbyte4 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.z),
        rhs.w == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.w)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator %(sbyte4 lhs, sbyte4 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs.x % rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs.y % rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs.z % rhs.z),
        rhs.w == 0 ? (sbyte)0 : (sbyte)(lhs.w % rhs.w)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator %(sbyte4 lhs, sbyte rhs) =>
      rhs == 0
        ? zero
        : new sbyte4(
          (sbyte)(lhs.x % rhs),
          (sbyte)(lhs.y % rhs),
          (sbyte)(lhs.z % rhs),
          (sbyte)(lhs.w % rhs)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator %(sbyte lhs, sbyte4 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.z),
        rhs.w == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.w)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator ++(sbyte4 val) =>
      new((sbyte)(val.x + 1), (sbyte)(val.y + 1), (sbyte)(val.z + 1), (sbyte)(val.w + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator --(sbyte4 val) =>
      new((sbyte)(val.x - 1), (sbyte)(val.y - 1), (sbyte)(val.z - 1), (sbyte)(val.w - 1));

    // Unary negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte4 operator -(sbyte4 val) =>
      new((sbyte)(-val.x), (sbyte)(-val.y), (sbyte)(-val.z), (sbyte)(-val.w));

    // Comparison operators - these return bool4 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(sbyte4 lhs, sbyte4 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z, lhs.w < rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(sbyte4 lhs, sbyte rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs, lhs.w < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <(sbyte lhs, sbyte4 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z, lhs < rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(sbyte4 lhs, sbyte4 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z, lhs.w <= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(sbyte4 lhs, sbyte rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs, lhs.w <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator <=(sbyte lhs, sbyte4 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z, lhs <= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(sbyte4 lhs, sbyte4 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z, lhs.w > rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(sbyte4 lhs, sbyte rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs, lhs.w > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >(sbyte lhs, sbyte4 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z, lhs > rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(sbyte4 lhs, sbyte4 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z, lhs.w >= rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(sbyte4 lhs, sbyte rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs, lhs.w >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator >=(sbyte lhs, sbyte4 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z, lhs >= rhs.w);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(sbyte4 lhs, sbyte4 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z, lhs.w == rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(sbyte4 lhs, sbyte rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs, lhs.w == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator ==(sbyte lhs, sbyte4 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z, lhs == rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(sbyte4 lhs, sbyte4 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z, lhs.w != rhs.w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(sbyte4 lhs, sbyte rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs, lhs.w != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4 operator !=(sbyte lhs, sbyte4 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z, lhs != rhs.w);

    // Basic swizzling properties - a small subset of the possible combinations
    public readonly sbyte2 xx => new(x, x);
    public readonly sbyte2 xy => new(x, y);
    public readonly sbyte2 xz => new(x, z);
    public readonly sbyte2 xw => new(x, w);
    public readonly sbyte2 yx => new(y, x);
    public readonly sbyte2 yy => new(y, y);
    public readonly sbyte2 yz => new(y, z);
    public readonly sbyte2 yw => new(y, w);
    public readonly sbyte2 zx => new(z, x);
    public readonly sbyte2 zy => new(z, y);
    public readonly sbyte2 zz => new(z, z);
    public readonly sbyte2 zw => new(z, w);
    public readonly sbyte2 wx => new(w, x);
    public readonly sbyte2 wy => new(w, y);
    public readonly sbyte2 wz => new(w, z);
    public readonly sbyte2 ww => new(w, w);

    // Common sbyte3 swizzles
    public readonly sbyte3 xyz => new(x, y, z);
    public readonly sbyte3 xyw => new(x, y, w);
    public readonly sbyte3 xzy => new(x, z, y);
    public readonly sbyte3 xzw => new(x, z, w);
    public readonly sbyte3 xwy => new(x, w, y);
    public readonly sbyte3 xwz => new(x, w, z);
    public readonly sbyte3 yxz => new(y, x, z);
    public readonly sbyte3 yxw => new(y, x, w);
    public readonly sbyte3 yzx => new(y, z, x);
    public readonly sbyte3 yzw => new(y, z, w);
    public readonly sbyte3 ywx => new(y, w, x);
    public readonly sbyte3 ywz => new(y, w, z);
    public readonly sbyte3 zxy => new(z, x, y);
    public readonly sbyte3 zxw => new(z, x, w);
    public readonly sbyte3 zyx => new(z, y, x);
    public readonly sbyte3 zyw => new(z, y, w);
    public readonly sbyte3 zwx => new(z, w, x);
    public readonly sbyte3 zwy => new(z, w, y);
    public readonly sbyte3 wxy => new(w, x, y);
    public readonly sbyte3 wxz => new(w, x, z);
    public readonly sbyte3 wyx => new(w, y, x);
    public readonly sbyte3 wyz => new(w, y, z);
    public readonly sbyte3 wzx => new(w, z, x);
    public readonly sbyte3 wzy => new(w, z, y);

    // Common 4-component swizzles
    public readonly sbyte4 xxxx => new(x, x, x, x);
    public readonly sbyte4 yyyy => new(y, y, y, y);
    public readonly sbyte4 zzzz => new(z, z, z, z);
    public readonly sbyte4 wwww => new(w, w, w, w);

    // Identity and permutations
    public readonly sbyte4 xyzw => new(x, y, z, w);
    public readonly sbyte4 wzyx => new(w, z, y, x);

    // RGB with different alpha configurations
    public readonly sbyte4 rgbx => new(x, y, z, 0);
    public readonly sbyte4 rgbw => new(x, y, z, w);

    // Color patterns with alpha
    public readonly sbyte4 xyza => new(x, y, z, sbyte.MaxValue);
    public readonly sbyte4 xxxw => new(x, x, x, w);
    public readonly sbyte4 xyzx => new(x, y, z, x);
    public readonly sbyte4 xyzy => new(x, y, z, y);
    public readonly sbyte4 xyzz => new(x, y, z, z);

    /// <summary>Returns the sbyte element at a specified index.</summary>
    public unsafe sbyte this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new ArgumentException("index must be between[0...3]");
#endif
        fixed (sbyte4* array = &this)
        {
          return ((sbyte*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new ArgumentException("index must be between[0...3]");
#endif
        fixed (sbyte* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the sbyte4 is equal to a given sbyte4, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(sbyte4 other) =>
      x == other.x && y == other.y && z == other.z && w == other.w;

    /// <summary>Returns true if the sbyte4 is equal to a given sbyte4, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is sbyte4 other && Equals(other);

    /// <summary>Returns a hash code for the sbyte4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z, w);

    /// <summary>Returns a string representation of the sbyte4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"sbyte4({x}, {y}, {z}, {w})";

    /// <summary>Returns a string representation of the sbyte4 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"sbyte4({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)}, {w.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public sbyte x;
      public sbyte y;
      public sbyte z;
      public sbyte w;

      public DebuggerProxy(sbyte4 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
        w = v.w;
      }
    }
  }
}
