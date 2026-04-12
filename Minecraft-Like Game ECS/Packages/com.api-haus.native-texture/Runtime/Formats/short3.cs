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
  public struct short3 : IEquatable<short3>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public short x;

    /// <summary>y component of the vector.</summary>
    public short y;

    /// <summary>z component of the vector.</summary>
    public short z;

    /// <summary>short3 zero value.</summary>
    public static readonly short3 zero = new(0, 0, 0);

    /// <summary>short3 one value (all components are 1).</summary>
    public static readonly short3 one = new(1, 1, 1);

    /// <summary>short3 maximum value (all components are short.MaxValue).</summary>
    public static readonly short3 max = new(short.MaxValue, short.MaxValue, short.MaxValue);

    /// <summary>short3 minimum value (all components are short.MinValue).</summary>
    public static readonly short3 min = new(short.MinValue, short.MinValue, short.MinValue);

    /// <summary>Constructs a short3 vector from three short values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(short x, short y, short z)
    {
      this.x = x;
      this.y = y;
      this.z = z;
    }

    /// <summary>Constructs a short3 vector from a single short value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(short v)
    {
      x = v;
      y = v;
      z = v;
    }

    /// <summary>Constructs a short3 vector from a short3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(short3 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
    }

    /// <summary>Constructs a short3 vector from a short2 vector and a short value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(short2 xy, short z)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
    }

    /// <summary>Constructs a short3 vector from a short value and a short2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(short x, short2 yz)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
    }

    /// <summary>Constructs a short3 vector from a float3 vector by truncating the components to the nearest short value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(float3 v)
    {
      x = (short)(v.x / 1f * short.MaxValue);
      y = (short)(v.y / 1f * short.MaxValue);
      z = (short)(v.z / 1f * short.MaxValue);
    }

    /// <summary>Constructs a short3 vector from a sbyte3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(sbyte3 v)
    {
      x = (short)((float)v.x / sbyte.MaxValue * short.MaxValue);
      y = (short)((float)v.y / sbyte.MaxValue * short.MaxValue);
      z = (short)((float)v.z / sbyte.MaxValue * short.MaxValue);
    }

    /// <summary>Constructs a short3 vector from a single int value by converting it to short and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(int v)
    {
      x = (short)v;
      y = (short)v;
      z = (short)v;
    }

    /// <summary>Constructs a short3 vector from an int3 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short3(int3 v)
    {
      x = (short)v.x;
      y = (short)v.y;
      z = (short)v.z;
    }

    /// <summary>Extracts the xy components from a short3 vector to create a short2.</summary>
    public readonly short2 xy
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => new(x, y);
    }

    /// <summary>Extracts the xy components explicitly from a short3 vector to create a short2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly short2 GetXY() => new(x, y);

    /// <summary>
    /// Creates a short3 from a float3 in the -1..1 range, mapping to -32768..32767
    /// Useful for high-precision normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 FromNormalized(float3 v) =>
      new()
      {
        x = (short)(v.x * short.MaxValue),
        y = (short)(v.y * short.MaxValue),
        z = (short)(v.z * short.MaxValue),
      };

    /// <summary>
    /// Converts a short3 value (-32768..32767) back to normalized float3 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float3 ToNormalized() =>
      new((float)x / short.MaxValue, (float)y / short.MaxValue, (float)z / short.MaxValue);

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short3(short v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short3(float3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short3(sbyte3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float3(short3 v) => ToFloat3(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float3 ToFloat3(short3 s3) =>
      new((float)s3.x / short.MaxValue, (float)s3.y / short.MaxValue, (float)s3.z / short.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short3(int3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short3(short4 v) => new(v.x, v.y, v.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short2(short3 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator *(short3 lhs, short3 rhs) =>
      new((short)(lhs.x * rhs.x), (short)(lhs.y * rhs.y), (short)(lhs.z * rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator *(short3 lhs, short rhs) =>
      new((short)(lhs.x * rhs), (short)(lhs.y * rhs), (short)(lhs.z * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator *(short lhs, short3 rhs) =>
      new((short)(lhs * rhs.x), (short)(lhs * rhs.y), (short)(lhs * rhs.z));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator +(short3 lhs, short3 rhs) =>
      new((short)(lhs.x + rhs.x), (short)(lhs.y + rhs.y), (short)(lhs.z + rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator +(short3 lhs, short rhs) =>
      new((short)(lhs.x + rhs), (short)(lhs.y + rhs), (short)(lhs.z + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator +(short lhs, short3 rhs) =>
      new((short)(lhs + rhs.x), (short)(lhs + rhs.y), (short)(lhs + rhs.z));

    // Subtraction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator -(short3 lhs, short3 rhs) =>
      new((short)(lhs.x - rhs.x), (short)(lhs.y - rhs.y), (short)(lhs.z - rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator -(short3 lhs, short rhs) =>
      new((short)(lhs.x - rhs), (short)(lhs.y - rhs), (short)(lhs.z - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator -(short lhs, short3 rhs) =>
      new((short)(lhs - rhs.x), (short)(lhs - rhs.y), (short)(lhs - rhs.z));

    // Unary negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator -(short3 val) =>
      new((short)(-val.x), (short)(-val.y), (short)(-val.z));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator /(short3 lhs, short3 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs.x / rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs.y / rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs.z / rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator /(short3 lhs, short rhs) =>
      rhs == 0
        ? zero
        : new short3((short)(lhs.x / rhs), (short)(lhs.y / rhs), (short)(lhs.z / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator /(short lhs, short3 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs / rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs / rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs / rhs.z)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator %(short3 lhs, short3 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs.x % rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs.y % rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs.z % rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator %(short3 lhs, short rhs) =>
      rhs == 0
        ? zero
        : new short3((short)(lhs.x % rhs), (short)(lhs.y % rhs), (short)(lhs.z % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator %(short lhs, short3 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs % rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs % rhs.y),
        rhs.z == 0 ? (short)0 : (short)(lhs % rhs.z)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator ++(short3 val) =>
      new((short)(val.x + 1), (short)(val.y + 1), (short)(val.z + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short3 operator --(short3 val) =>
      new((short)(val.x - 1), (short)(val.y - 1), (short)(val.z - 1));

    // Comparison operators - these return bool3 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(short3 lhs, short3 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(short3 lhs, short rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(short lhs, short3 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(short3 lhs, short3 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(short3 lhs, short rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(short lhs, short3 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(short3 lhs, short3 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(short3 lhs, short rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(short lhs, short3 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(short3 lhs, short3 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(short3 lhs, short rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(short lhs, short3 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(short3 lhs, short3 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(short3 lhs, short rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(short lhs, short3 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(short3 lhs, short3 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(short3 lhs, short rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(short lhs, short3 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z);

    // Swizzling properties
    public readonly short2 xx => new(x, x);

    /* xy property is defined earlier as a standard component accessor */
    public readonly short2 xz => new(x, z);
    public readonly short2 yx => new(y, x);
    public readonly short2 yy => new(y, y);
    public readonly short2 yz => new(y, z);
    public readonly short2 zx => new(z, x);
    public readonly short2 zy => new(z, y);
    public readonly short2 zz => new(z, z);

    // 3-component swizzles
    public readonly short3 xxx => new(x, x, x);
    public readonly short3 xxy => new(x, x, y);
    public readonly short3 xxz => new(x, x, z);
    public readonly short3 xyx => new(x, y, x);
    public readonly short3 xyy => new(x, y, y);
    public readonly short3 xyz => new(x, y, z);
    public readonly short3 xzx => new(x, z, x);
    public readonly short3 xzy => new(x, z, y);
    public readonly short3 xzz => new(x, z, z);

    public readonly short3 yxx => new(y, x, x);
    public readonly short3 yxy => new(y, x, y);
    public readonly short3 yxz => new(y, x, z);
    public readonly short3 yyx => new(y, y, x);
    public readonly short3 yyy => new(y, y, y);
    public readonly short3 yyz => new(y, y, z);
    public readonly short3 yzx => new(y, z, x);
    public readonly short3 yzy => new(y, z, y);
    public readonly short3 yzz => new(y, z, z);

    public readonly short3 zxx => new(z, x, x);
    public readonly short3 zxy => new(z, x, y);
    public readonly short3 zxz => new(z, x, z);
    public readonly short3 zyx => new(z, y, x);
    public readonly short3 zyy => new(z, y, y);
    public readonly short3 zyz => new(z, y, z);
    public readonly short3 zzx => new(z, z, x);
    public readonly short3 zzy => new(z, z, y);
    public readonly short3 zzz => new(z, z, z);

    /// <summary>Returns the short element at a specified index.</summary>
    public unsafe short this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (short3* array = &this)
        {
          return ((short*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (short* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the short3 is equal to a given short3, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(short3 other) => x == other.x && y == other.y && z == other.z;

    /// <summary>Returns true if the short3 is equal to a given short3, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is short3 other && Equals(other);

    /// <summary>Returns a hash code for the short3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z);

    /// <summary>Returns a string representation of the short3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"short3({x}, {y}, {z})";

    /// <summary>Returns a string representation of the short3 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"short3({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public short x;
      public short y;
      public short z;

      public DebuggerProxy(short3 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
      }
    }
  }
}
