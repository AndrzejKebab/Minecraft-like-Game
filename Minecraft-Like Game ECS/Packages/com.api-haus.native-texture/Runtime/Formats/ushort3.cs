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
  public struct ushort3 : IEquatable<ushort3>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public ushort x;

    /// <summary>y component of the vector.</summary>
    public ushort y;

    /// <summary>z component of the vector.</summary>
    public ushort z;

    /// <summary>ushort3 zero value.</summary>
    public static readonly ushort3 zero = new(0, 0, 0);

    /// <summary>ushort3 one value (all components are 1).</summary>
    public static readonly ushort3 one = new(1, 1, 1);

    /// <summary>ushort3 maximum value (all components are ushort.MaxValue).</summary>
    public static readonly ushort3 max = new(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

    /// <summary>Constructs a ushort3 vector from three ushort values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(ushort x, ushort y, ushort z)
    {
      this.x = x;
      this.y = y;
      this.z = z;
    }

    /// <summary>Constructs a ushort3 vector from a single ushort value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(ushort v)
    {
      x = v;
      y = v;
      z = v;
    }

    /// <summary>Constructs a ushort3 vector from a ushort3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(ushort3 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
    }

    /// <summary>Constructs a ushort3 vector from a ushort2 vector and a ushort value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(ushort2 xy, ushort z)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
    }

    /// <summary>Constructs a ushort3 vector from a ushort value and a ushort2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(ushort x, ushort2 yz)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
    }

    /// <summary>Constructs a ushort3 vector from a float3 vector by truncating the components to the nearest ushort value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(float3 v)
    {
      x = (ushort)(v.x / 1f * ushort.MaxValue);
      y = (ushort)(v.y / 1f * ushort.MaxValue);
      z = (ushort)(v.z / 1f * ushort.MaxValue);
    }

    /// <summary>Constructs a ushort3 vector from a byte3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(byte3 v)
    {
      x = (ushort)((float)v.x / byte.MaxValue * ushort.MaxValue);
      y = (ushort)((float)v.y / byte.MaxValue * ushort.MaxValue);
      z = (ushort)((float)v.z / byte.MaxValue * ushort.MaxValue);
    }

    /// <summary>Constructs a ushort3 vector from a single int value by converting it to ushort and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(int v)
    {
      x = (ushort)v;
      y = (ushort)v;
      z = (ushort)v;
    }

    /// <summary>Constructs a ushort3 vector from an int3 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(int3 v)
    {
      x = (ushort)v.x;
      y = (ushort)v.y;
      z = (ushort)v.z;
    }

    /// <summary>Constructs a ushort3 vector from a single uint value by converting it to ushort and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(uint v)
    {
      x = (ushort)v;
      y = (ushort)v;
      z = (ushort)v;
    }

    /// <summary>Constructs a ushort3 vector from a uint3 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort3(uint3 v)
    {
      x = (ushort)v.x;
      y = (ushort)v.y;
      z = (ushort)v.z;
    }

    /// <summary>Extracts the xy components from a ushort3 vector to create a ushort2.</summary>
    public readonly ushort2 xy
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => new(x, y);
    }

    /// <summary>Extracts the xy components explicitly from a ushort3 vector to create a ushort2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ushort2 GetXY() => new(x, y);

    /// <summary>
    /// Creates a ushort3 from a float3 in the -1..1 range, mapping to 0..65535
    /// Useful for high-precision normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 FromNormalized(float3 v) =>
      new()
      {
        x = (ushort)(((v.x * 0.5f) + 0.5f) * ushort.MaxValue),
        y = (ushort)(((v.y * 0.5f) + 0.5f) * ushort.MaxValue),
        z = (ushort)(((v.z * 0.5f) + 0.5f) * ushort.MaxValue),
      };

    /// <summary>
    /// Converts a ushort3 value (0..65535) back to normalized float3 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float3 ToNormalized() =>
      new(
        ((float)x / ushort.MaxValue * 2f) - 1f,
        ((float)y / ushort.MaxValue * 2f) - 1f,
        ((float)z / ushort.MaxValue * 2f) - 1f
      );

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort3(ushort v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort3(float3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort3(byte3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float3(ushort3 v) => ToFloat3(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float3 ToFloat3(ushort3 u3) =>
      new(
        (float)u3.x / ushort.MaxValue,
        (float)u3.y / ushort.MaxValue,
        (float)u3.z / ushort.MaxValue
      );

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort3(int3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort3(uint3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort3(ushort4 v) => new(v.x, v.y, v.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort2(ushort3 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator *(ushort3 lhs, ushort3 rhs) =>
      new((ushort)(lhs.x * rhs.x), (ushort)(lhs.y * rhs.y), (ushort)(lhs.z * rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator *(ushort3 lhs, ushort rhs) =>
      new((ushort)(lhs.x * rhs), (ushort)(lhs.y * rhs), (ushort)(lhs.z * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator *(ushort lhs, ushort3 rhs) =>
      new((ushort)(lhs * rhs.x), (ushort)(lhs * rhs.y), (ushort)(lhs * rhs.z));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator +(ushort3 lhs, ushort3 rhs) =>
      new((ushort)(lhs.x + rhs.x), (ushort)(lhs.y + rhs.y), (ushort)(lhs.z + rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator +(ushort3 lhs, ushort rhs) =>
      new((ushort)(lhs.x + rhs), (ushort)(lhs.y + rhs), (ushort)(lhs.z + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator +(ushort lhs, ushort3 rhs) =>
      new((ushort)(lhs + rhs.x), (ushort)(lhs + rhs.y), (ushort)(lhs + rhs.z));

    // Subtraction (with clamping to zero)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator -(ushort3 lhs, ushort3 rhs) =>
      new(
        (ushort)Math.Max(0, lhs.x - rhs.x),
        (ushort)Math.Max(0, lhs.y - rhs.y),
        (ushort)Math.Max(0, lhs.z - rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator -(ushort3 lhs, ushort rhs) =>
      new(
        (ushort)Math.Max(0, lhs.x - rhs),
        (ushort)Math.Max(0, lhs.y - rhs),
        (ushort)Math.Max(0, lhs.z - rhs)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator -(ushort lhs, ushort3 rhs) =>
      new(
        (ushort)Math.Max(0, lhs - rhs.x),
        (ushort)Math.Max(0, lhs - rhs.y),
        (ushort)Math.Max(0, lhs - rhs.z)
      );

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator /(ushort3 lhs, ushort3 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs.x / rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs.y / rhs.y),
        rhs.z == 0 ? (ushort)0 : (ushort)(lhs.z / rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator /(ushort3 lhs, ushort rhs) =>
      rhs == 0
        ? zero
        : new ushort3((ushort)(lhs.x / rhs), (ushort)(lhs.y / rhs), (ushort)(lhs.z / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator /(ushort lhs, ushort3 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs / rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs / rhs.y),
        rhs.z == 0 ? (ushort)0 : (ushort)(lhs / rhs.z)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator %(ushort3 lhs, ushort3 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs.x % rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs.y % rhs.y),
        rhs.z == 0 ? (ushort)0 : (ushort)(lhs.z % rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator %(ushort3 lhs, ushort rhs) =>
      rhs == 0
        ? zero
        : new ushort3((ushort)(lhs.x % rhs), (ushort)(lhs.y % rhs), (ushort)(lhs.z % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator %(ushort lhs, ushort3 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs % rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs % rhs.y),
        rhs.z == 0 ? (ushort)0 : (ushort)(lhs % rhs.z)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator ++(ushort3 val) =>
      new((ushort)(val.x + 1), (ushort)(val.y + 1), (ushort)(val.z + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort3 operator --(ushort3 val) =>
      new(
        (ushort)Math.Max(0, val.x - 1),
        (ushort)Math.Max(0, val.y - 1),
        (ushort)Math.Max(0, val.z - 1)
      );

    // Comparison operators - these return bool3 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(ushort3 lhs, ushort3 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(ushort3 lhs, ushort rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(ushort lhs, ushort3 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(ushort3 lhs, ushort3 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(ushort3 lhs, ushort rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(ushort lhs, ushort3 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(ushort3 lhs, ushort3 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(ushort3 lhs, ushort rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(ushort lhs, ushort3 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(ushort3 lhs, ushort3 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(ushort3 lhs, ushort rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(ushort lhs, ushort3 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(ushort3 lhs, ushort3 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(ushort3 lhs, ushort rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(ushort lhs, ushort3 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(ushort3 lhs, ushort3 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(ushort3 lhs, ushort rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(ushort lhs, ushort3 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z);

    // Swizzling properties
    public readonly ushort2 xx => new(x, x);

    /* xy property is defined earlier as a standard component accessor */
    public readonly ushort2 xz => new(x, z);
    public readonly ushort2 yx => new(y, x);
    public readonly ushort2 yy => new(y, y);
    public readonly ushort2 yz => new(y, z);
    public readonly ushort2 zx => new(z, x);
    public readonly ushort2 zy => new(z, y);
    public readonly ushort2 zz => new(z, z);

    // 3-component swizzles
    public readonly ushort3 xxx => new(x, x, x);
    public readonly ushort3 xxy => new(x, x, y);
    public readonly ushort3 xxz => new(x, x, z);
    public readonly ushort3 xyx => new(x, y, x);
    public readonly ushort3 xyy => new(x, y, y);
    public readonly ushort3 xyz => new(x, y, z);
    public readonly ushort3 xzx => new(x, z, x);
    public readonly ushort3 xzy => new(x, z, y);
    public readonly ushort3 xzz => new(x, z, z);

    public readonly ushort3 yxx => new(y, x, x);
    public readonly ushort3 yxy => new(y, x, y);
    public readonly ushort3 yxz => new(y, x, z);
    public readonly ushort3 yyx => new(y, y, x);
    public readonly ushort3 yyy => new(y, y, y);
    public readonly ushort3 yyz => new(y, y, z);
    public readonly ushort3 yzx => new(y, z, x);
    public readonly ushort3 yzy => new(y, z, y);
    public readonly ushort3 yzz => new(y, z, z);

    public readonly ushort3 zxx => new(z, x, x);
    public readonly ushort3 zxy => new(z, x, y);
    public readonly ushort3 zxz => new(z, x, z);
    public readonly ushort3 zyx => new(z, y, x);
    public readonly ushort3 zyy => new(z, y, y);
    public readonly ushort3 zyz => new(z, y, z);
    public readonly ushort3 zzx => new(z, z, x);
    public readonly ushort3 zzy => new(z, z, y);
    public readonly ushort3 zzz => new(z, z, z);

    /// <summary>Returns the ushort element at a specified index.</summary>
    public unsafe ushort this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (ushort3* array = &this)
        {
          return ((ushort*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (ushort* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the ushort3 is equal to a given ushort3, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(ushort3 other) => x == other.x && y == other.y && z == other.z;

    /// <summary>Returns true if the ushort3 is equal to a given ushort3, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is ushort3 other && Equals(other);

    /// <summary>Returns a hash code for the ushort3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z);

    /// <summary>Returns a string representation of the ushort3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"ushort3({x}, {y}, {z})";

    /// <summary>Returns a string representation of the ushort3 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"ushort3({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public ushort x;
      public ushort y;
      public ushort z;

      public DebuggerProxy(ushort3 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
      }
    }
  }
}
