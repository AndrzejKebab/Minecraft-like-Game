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
  public struct sbyte3 : IEquatable<sbyte3>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public sbyte x;

    /// <summary>y component of the vector.</summary>
    public sbyte y;

    /// <summary>z component of the vector.</summary>
    public sbyte z;

    /// <summary>sbyte3 zero value.</summary>
    public static readonly sbyte3 zero = new(0, 0, 0);

    /// <summary>sbyte3 one value (all components are 1).</summary>
    public static readonly sbyte3 one = new(1, 1, 1);

    /// <summary>sbyte3 maximum value (all components are sbyte.MaxValue).</summary>
    public static readonly sbyte3 max = new(sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue);

    /// <summary>sbyte3 minimum value (all components are sbyte.MinValue).</summary>
    public static readonly sbyte3 min = new(sbyte.MinValue, sbyte.MinValue, sbyte.MinValue);

    /// <summary>Constructs a sbyte3 vector from three sbyte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(sbyte x, sbyte y, sbyte z)
    {
      this.x = x;
      this.y = y;
      this.z = z;
    }

    /// <summary>Constructs a sbyte3 vector from a single sbyte value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(sbyte v)
    {
      x = v;
      y = v;
      z = v;
    }

    /// <summary>Constructs a sbyte3 vector from a sbyte3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(sbyte3 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
    }

    /// <summary>Constructs a sbyte3 vector from a sbyte2 vector and a sbyte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(sbyte2 xy, sbyte z)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
    }

    /// <summary>Constructs a sbyte3 vector from a sbyte value and a sbyte2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(sbyte x, sbyte2 yz)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
    }

    /// <summary>Constructs a sbyte3 vector from a float3 vector by truncating the components to the nearest sbyte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(float3 v)
    {
      x = (sbyte)(v.x / 1f * sbyte.MaxValue);
      y = (sbyte)(v.y / 1f * sbyte.MaxValue);
      z = (sbyte)(v.z / 1f * sbyte.MaxValue);
    }

    /// <summary>Constructs a sbyte3 vector from a short3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(short3 v)
    {
      x = (sbyte)((float)v.x / short.MaxValue * sbyte.MaxValue);
      y = (sbyte)((float)v.y / short.MaxValue * sbyte.MaxValue);
      z = (sbyte)((float)v.z / short.MaxValue * sbyte.MaxValue);
    }

    /// <summary>Constructs a sbyte3 vector from a single int value by converting it to sbyte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(int v)
    {
      x = (sbyte)v;
      y = (sbyte)v;
      z = (sbyte)v;
    }

    /// <summary>Constructs a sbyte3 vector from an int3 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte3(int3 v)
    {
      x = (sbyte)v.x;
      y = (sbyte)v.y;
      z = (sbyte)v.z;
    }

    /// <summary>Extracts the xy components from a sbyte3 vector to create a sbyte2.</summary>
    public readonly sbyte2 xy
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => new(x, y);
    }

    /// <summary>Extracts the xy components (explicitly) from a sbyte3 vector to create a sbyte2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly sbyte2 GetXY() => new(x, y);

    /// <summary>
    /// Creates a sbyte3 from a float3 in the -1..1 range, mapping to -128..127
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 FromNormalized(float3 v) =>
      new()
      {
        x = (sbyte)(v.x * sbyte.MaxValue),
        y = (sbyte)(v.y * sbyte.MaxValue),
        z = (sbyte)(v.z * sbyte.MaxValue),
      };

    /// <summary>
    /// Converts a sbyte3 value (-128..127) back to normalized float3 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float3 ToNormalized() =>
      new((float)x / sbyte.MaxValue, (float)y / sbyte.MaxValue, (float)z / sbyte.MaxValue);

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte3(sbyte v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte3(float3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte3(short3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float3(sbyte3 v) => ToFloat3(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float3 ToFloat3(sbyte3 s3) =>
      new((float)s3.x / sbyte.MaxValue, (float)s3.y / sbyte.MaxValue, (float)s3.z / sbyte.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte3(int3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte3(sbyte4 v) => new(v.x, v.y, v.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte2(sbyte3 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator *(sbyte3 lhs, sbyte3 rhs) =>
      new((sbyte)(lhs.x * rhs.x), (sbyte)(lhs.y * rhs.y), (sbyte)(lhs.z * rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator *(sbyte3 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x * rhs), (sbyte)(lhs.y * rhs), (sbyte)(lhs.z * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator *(sbyte lhs, sbyte3 rhs) =>
      new((sbyte)(lhs * rhs.x), (sbyte)(lhs * rhs.y), (sbyte)(lhs * rhs.z));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator +(sbyte3 lhs, sbyte3 rhs) =>
      new((sbyte)(lhs.x + rhs.x), (sbyte)(lhs.y + rhs.y), (sbyte)(lhs.z + rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator +(sbyte3 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x + rhs), (sbyte)(lhs.y + rhs), (sbyte)(lhs.z + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator +(sbyte lhs, sbyte3 rhs) =>
      new((sbyte)(lhs + rhs.x), (sbyte)(lhs + rhs.y), (sbyte)(lhs + rhs.z));

    // Subtraction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator -(sbyte3 lhs, sbyte3 rhs) =>
      new((sbyte)(lhs.x - rhs.x), (sbyte)(lhs.y - rhs.y), (sbyte)(lhs.z - rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator -(sbyte3 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x - rhs), (sbyte)(lhs.y - rhs), (sbyte)(lhs.z - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator -(sbyte lhs, sbyte3 rhs) =>
      new((sbyte)(lhs - rhs.x), (sbyte)(lhs - rhs.y), (sbyte)(lhs - rhs.z));

    // Unary negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator -(sbyte3 val) =>
      new((sbyte)(-val.x), (sbyte)(-val.y), (sbyte)(-val.z));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator /(sbyte3 lhs, sbyte3 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs.x / rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs.y / rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs.z / rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator /(sbyte3 lhs, sbyte rhs) =>
      rhs == 0
        ? zero
        : new sbyte3((sbyte)(lhs.x / rhs), (sbyte)(lhs.y / rhs), (sbyte)(lhs.z / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator /(sbyte lhs, sbyte3 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.z)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator %(sbyte3 lhs, sbyte3 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs.x % rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs.y % rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs.z % rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator %(sbyte3 lhs, sbyte rhs) =>
      rhs == 0
        ? zero
        : new sbyte3((sbyte)(lhs.x % rhs), (sbyte)(lhs.y % rhs), (sbyte)(lhs.z % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator %(sbyte lhs, sbyte3 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.y),
        rhs.z == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.z)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator ++(sbyte3 val) =>
      new((sbyte)(val.x + 1), (sbyte)(val.y + 1), (sbyte)(val.z + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte3 operator --(sbyte3 val) =>
      new((sbyte)(val.x - 1), (sbyte)(val.y - 1), (sbyte)(val.z - 1));

    // Comparison operators - these return bool3 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(sbyte3 lhs, sbyte3 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(sbyte3 lhs, sbyte rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(sbyte lhs, sbyte3 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(sbyte3 lhs, sbyte3 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(sbyte3 lhs, sbyte rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(sbyte lhs, sbyte3 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(sbyte3 lhs, sbyte3 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(sbyte3 lhs, sbyte rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(sbyte lhs, sbyte3 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(sbyte3 lhs, sbyte3 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(sbyte3 lhs, sbyte rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(sbyte lhs, sbyte3 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(sbyte3 lhs, sbyte3 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(sbyte3 lhs, sbyte rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(sbyte lhs, sbyte3 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(sbyte3 lhs, sbyte3 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(sbyte3 lhs, sbyte rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(sbyte lhs, sbyte3 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z);

    // Swizzling properties
    public readonly sbyte2 xx => new(x, x);

    /* xy property is defined earlier as a standard component accessor */
    public readonly sbyte2 xz => new(x, z);

    public readonly sbyte2 yx => new(y, x);

    public readonly sbyte2 yy => new(y, y);

    public readonly sbyte2 yz => new(y, z);

    public readonly sbyte2 zx => new(z, x);

    public readonly sbyte2 zy => new(z, y);

    public readonly sbyte2 zz => new(z, z);

    // 3-component swizzles
    public readonly sbyte3 xxx => new(x, x, x);
    public readonly sbyte3 xxy => new(x, x, y);
    public readonly sbyte3 xxz => new(x, x, z);
    public readonly sbyte3 xyx => new(x, y, x);
    public readonly sbyte3 xyy => new(x, y, y);
    public readonly sbyte3 xyz => new(x, y, z);
    public readonly sbyte3 xzx => new(x, z, x);
    public readonly sbyte3 xzy => new(x, z, y);
    public readonly sbyte3 xzz => new(x, z, z);

    public readonly sbyte3 yxx => new(y, x, x);
    public readonly sbyte3 yxy => new(y, x, y);
    public readonly sbyte3 yxz => new(y, x, z);
    public readonly sbyte3 yyx => new(y, y, x);
    public readonly sbyte3 yyy => new(y, y, y);
    public readonly sbyte3 yyz => new(y, y, z);
    public readonly sbyte3 yzx => new(y, z, x);
    public readonly sbyte3 yzy => new(y, z, y);
    public readonly sbyte3 yzz => new(y, z, z);

    public readonly sbyte3 zxx => new(z, x, x);
    public readonly sbyte3 zxy => new(z, x, y);
    public readonly sbyte3 zxz => new(z, x, z);
    public readonly sbyte3 zyx => new(z, y, x);
    public readonly sbyte3 zyy => new(z, y, y);
    public readonly sbyte3 zyz => new(z, y, z);
    public readonly sbyte3 zzx => new(z, z, x);
    public readonly sbyte3 zzy => new(z, z, y);
    public readonly sbyte3 zzz => new(z, z, z);

    /// <summary>Returns the sbyte element at a specified index.</summary>
    public unsafe sbyte this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (sbyte3* array = &this)
        {
          return ((sbyte*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (sbyte* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the sbyte3 is equal to a given sbyte3, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(sbyte3 other) => x == other.x && y == other.y && z == other.z;

    /// <summary>Returns true if the sbyte3 is equal to a given sbyte3, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is sbyte3 other && Equals(other);

    /// <summary>Returns a hash code for the sbyte3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z);

    /// <summary>Returns a string representation of the sbyte3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"sbyte3({x}, {y}, {z})";

    /// <summary>Returns a string representation of the sbyte3 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"sbyte3({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public sbyte x;
      public sbyte y;
      public sbyte z;

      public DebuggerProxy(sbyte3 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
      }
    }
  }
}
