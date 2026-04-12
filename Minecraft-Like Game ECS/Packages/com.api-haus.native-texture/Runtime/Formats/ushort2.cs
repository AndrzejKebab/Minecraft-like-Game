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
  public struct ushort2 : IEquatable<ushort2>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public ushort x;

    /// <summary>y component of the vector.</summary>
    public ushort y;

    /// <summary>ushort2 zero value.</summary>
    public static readonly ushort2 zero = new(0, 0);

    /// <summary>ushort2 one value (all components are 1).</summary>
    public static readonly ushort2 one = new(1, 1);

    /// <summary>ushort2 maximum value (all components are ushort.MaxValue).</summary>
    public static readonly ushort2 max = new(ushort.MaxValue, ushort.MaxValue);

    /// <summary>Constructs a ushort2 vector from two ushort values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(ushort x, ushort y)
    {
      this.x = x;
      this.y = y;
    }

    /// <summary>Constructs a ushort2 vector from a single ushort value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(ushort v)
    {
      x = v;
      y = v;
    }

    /// <summary>Constructs a ushort2 vector from a ushort2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(ushort2 v)
    {
      x = v.x;
      y = v.y;
    }

    /// <summary>Constructs a ushort2 vector from a float2 vector by truncating the components to the nearest ushort value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(float2 v)
    {
      x = (ushort)(v.x / 1f * ushort.MaxValue);
      y = (ushort)(v.y / 1f * ushort.MaxValue);
    }

    /// <summary>Constructs a ushort2 vector from a byte2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(byte2 v)
    {
      x = (ushort)((float)v.x / byte.MaxValue * ushort.MaxValue);
      y = (ushort)((float)v.y / byte.MaxValue * ushort.MaxValue);
    }

    /// <summary>Constructs a ushort2 vector from a single int value by converting it to ushort and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(int v)
    {
      x = (ushort)v;
      y = (ushort)v;
    }

    /// <summary>Constructs a ushort2 vector from an int2 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(int2 v)
    {
      x = (ushort)v.x;
      y = (ushort)v.y;
    }

    /// <summary>Constructs a ushort2 vector from a single uint value by converting it to ushort and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(uint v)
    {
      x = (ushort)v;
      y = (ushort)v;
    }

    /// <summary>Constructs a ushort2 vector from a uint2 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort2(uint2 v)
    {
      x = (ushort)v.x;
      y = (ushort)v.y;
    }

    /// <summary>
    /// Creates a ushort2 from a float2 in the -1..1 range, mapping to 0..65535
    /// Useful for high-precision normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 FromNormalized(float2 v) =>
      new()
      {
        x = (ushort)(((v.x * 0.5f) + 0.5f) * ushort.MaxValue),
        y = (ushort)(((v.y * 0.5f) + 0.5f) * ushort.MaxValue),
      };

    /// <summary>
    /// Converts a ushort2 value (0..65535) back to normalized float2 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float2 ToNormalized() =>
      new(((float)x / ushort.MaxValue * 2f) - 1f, ((float)y / ushort.MaxValue * 2f) - 1f);

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort2(ushort v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort2(float2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort2(byte2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float2(ushort2 v) => ToFloat2(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 ToFloat2(ushort2 u2) =>
      new((float)u2.x / ushort.MaxValue, (float)u2.y / ushort.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort2(int2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort2(uint2 v) => new(v);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator *(ushort2 lhs, ushort2 rhs) =>
      new((ushort)(lhs.x * rhs.x), (ushort)(lhs.y * rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator *(ushort2 lhs, ushort rhs) =>
      new((ushort)(lhs.x * rhs), (ushort)(lhs.y * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator *(ushort lhs, ushort2 rhs) =>
      new((ushort)(lhs * rhs.x), (ushort)(lhs * rhs.y));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator +(ushort2 lhs, ushort2 rhs) =>
      new((ushort)(lhs.x + rhs.x), (ushort)(lhs.y + rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator +(ushort2 lhs, ushort rhs) =>
      new((ushort)(lhs.x + rhs), (ushort)(lhs.y + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator +(ushort lhs, ushort2 rhs) =>
      new((ushort)(lhs + rhs.x), (ushort)(lhs + rhs.y));

    // Subtraction (with clamping to zero)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator -(ushort2 lhs, ushort2 rhs) =>
      new((ushort)Math.Max(0, lhs.x - rhs.x), (ushort)Math.Max(0, lhs.y - rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator -(ushort2 lhs, ushort rhs) =>
      new((ushort)Math.Max(0, lhs.x - rhs), (ushort)Math.Max(0, lhs.y - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator -(ushort lhs, ushort2 rhs) =>
      new((ushort)Math.Max(0, lhs - rhs.x), (ushort)Math.Max(0, lhs - rhs.y));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator /(ushort2 lhs, ushort2 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs.x / rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs.y / rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator /(ushort2 lhs, ushort rhs) =>
      rhs == 0 ? zero : new ushort2((ushort)(lhs.x / rhs), (ushort)(lhs.y / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator /(ushort lhs, ushort2 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs / rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs / rhs.y)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator %(ushort2 lhs, ushort2 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs.x % rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs.y % rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator %(ushort2 lhs, ushort rhs) =>
      rhs == 0 ? zero : new ushort2((ushort)(lhs.x % rhs), (ushort)(lhs.y % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator %(ushort lhs, ushort2 rhs) =>
      new(
        rhs.x == 0 ? (ushort)0 : (ushort)(lhs % rhs.x),
        rhs.y == 0 ? (ushort)0 : (ushort)(lhs % rhs.y)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator ++(ushort2 val) => new((ushort)(val.x + 1), (ushort)(val.y + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort2 operator --(ushort2 val) =>
      new((ushort)Math.Max(0, val.x - 1), (ushort)Math.Max(0, val.y - 1));

    // Comparison operators - these return bool2 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(ushort2 lhs, ushort2 rhs) => new(lhs.x < rhs.x, lhs.y < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(ushort2 lhs, ushort rhs) => new(lhs.x < rhs, lhs.y < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(ushort lhs, ushort2 rhs) => new(lhs < rhs.x, lhs < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(ushort2 lhs, ushort2 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(ushort2 lhs, ushort rhs) => new(lhs.x <= rhs, lhs.y <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(ushort lhs, ushort2 rhs) => new(lhs <= rhs.x, lhs <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(ushort2 lhs, ushort2 rhs) => new(lhs.x > rhs.x, lhs.y > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(ushort2 lhs, ushort rhs) => new(lhs.x > rhs, lhs.y > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(ushort lhs, ushort2 rhs) => new(lhs > rhs.x, lhs > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(ushort2 lhs, ushort2 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(ushort2 lhs, ushort rhs) => new(lhs.x >= rhs, lhs.y >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(ushort lhs, ushort2 rhs) => new(lhs >= rhs.x, lhs >= rhs.y);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(ushort2 lhs, ushort2 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(ushort2 lhs, ushort rhs) => new(lhs.x == rhs, lhs.y == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(ushort lhs, ushort2 rhs) => new(lhs == rhs.x, lhs == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(ushort2 lhs, ushort2 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(ushort2 lhs, ushort rhs) => new(lhs.x != rhs, lhs.y != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(ushort lhs, ushort2 rhs) => new(lhs != rhs.x, lhs != rhs.y);

    // Swizzling properties
    public readonly ushort2 xx => new(x, x);
    public readonly ushort2 xy => new(x, y);
    public readonly ushort2 yx => new(y, x);
    public readonly ushort2 yy => new(y, y);

    /// <summary>Returns the ushort element at a specified index.</summary>
    public unsafe ushort this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (ushort2* array = &this)
        {
          return ((ushort*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (ushort* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the ushort2 is equal to a given ushort2, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(ushort2 other) => x == other.x && y == other.y;

    /// <summary>Returns true if the ushort2 is equal to a given ushort2, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is ushort2 other && Equals(other);

    /// <summary>Returns a hash code for the ushort2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y);

    /// <summary>Returns a string representation of the ushort2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"ushort2({x}, {y})";

    /// <summary>Returns a string representation of the ushort2 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"ushort2({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public ushort x;
      public ushort y;

      public DebuggerProxy(ushort2 v)
      {
        x = v.x;
        y = v.y;
      }
    }
  }
}
