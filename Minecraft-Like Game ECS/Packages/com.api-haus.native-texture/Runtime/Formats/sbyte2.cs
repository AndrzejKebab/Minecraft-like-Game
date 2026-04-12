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
  public struct sbyte2 : IEquatable<sbyte2>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public sbyte x;

    /// <summary>y component of the vector.</summary>
    public sbyte y;

    /// <summary>sbyte2 zero value.</summary>
    public static readonly sbyte2 zero = new(0, 0);

    /// <summary>sbyte2 one value (all components are 1).</summary>
    public static readonly sbyte2 one = new(1, 1);

    /// <summary>sbyte2 maximum value (all components are sbyte.MaxValue).</summary>
    public static readonly sbyte2 max = new(sbyte.MaxValue, sbyte.MaxValue);

    /// <summary>sbyte2 minimum value (all components are sbyte.MinValue).</summary>
    public static readonly sbyte2 min = new(sbyte.MinValue, sbyte.MinValue);

    /// <summary>Constructs a sbyte2 vector from two sbyte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(sbyte x, sbyte y)
    {
      this.x = x;
      this.y = y;
    }

    /// <summary>Constructs a sbyte2 vector from a single sbyte value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(sbyte v)
    {
      x = v;
      y = v;
    }

    /// <summary>Constructs a sbyte2 vector from a sbyte2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(sbyte2 v)
    {
      x = v.x;
      y = v.y;
    }

    /// <summary>Constructs a sbyte2 vector from a float2 vector by truncating the components to the nearest sbyte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(float2 v)
    {
      x = (sbyte)(v.x / 1f * sbyte.MaxValue);
      y = (sbyte)(v.y / 1f * sbyte.MaxValue);
    }

    /// <summary>Constructs a sbyte2 vector from a short2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(short2 v)
    {
      x = (sbyte)((float)v.x / short.MaxValue * sbyte.MaxValue);
      y = (sbyte)((float)v.y / short.MaxValue * sbyte.MaxValue);
    }

    /// <summary>Constructs a sbyte2 vector from a single int value by converting it to sbyte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(int v)
    {
      x = (sbyte)v;
      y = (sbyte)v;
    }

    /// <summary>Constructs a sbyte2 vector from an int2 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte2(int2 v)
    {
      x = (sbyte)v.x;
      y = (sbyte)v.y;
    }

    /// <summary>
    /// Creates a sbyte2 from a float2 in the -1..1 range, mapping to -128..127
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 FromNormalized(float2 v) =>
      new() { x = (sbyte)(v.x * sbyte.MaxValue), y = (sbyte)(v.y * sbyte.MaxValue) };

    /// <summary>
    /// Converts a sbyte2 value (-128..127) back to normalized float2 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float2 ToNormalized() =>
      new((float)x / sbyte.MaxValue, (float)y / sbyte.MaxValue);

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte2(sbyte v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte2(float2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte2(short2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float2(sbyte2 v) => ToFloat2(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 ToFloat2(sbyte2 s2) =>
      new((float)s2.x / sbyte.MaxValue, (float)s2.y / sbyte.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte2(int2 v) => new((sbyte)v.x, (sbyte)v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte2(sbyte3 v) => new(v.x, v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sbyte2(sbyte4 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator *(sbyte2 lhs, sbyte2 rhs) =>
      new((sbyte)(lhs.x * rhs.x), (sbyte)(lhs.y * rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator *(sbyte2 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x * rhs), (sbyte)(lhs.y * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator *(sbyte lhs, sbyte2 rhs) =>
      new((sbyte)(lhs * rhs.x), (sbyte)(lhs * rhs.y));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator +(sbyte2 lhs, sbyte2 rhs) =>
      new((sbyte)(lhs.x + rhs.x), (sbyte)(lhs.y + rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator +(sbyte2 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x + rhs), (sbyte)(lhs.y + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator +(sbyte lhs, sbyte2 rhs) =>
      new((sbyte)(lhs + rhs.x), (sbyte)(lhs + rhs.y));

    // Subtraction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator -(sbyte2 lhs, sbyte2 rhs) =>
      new((sbyte)(lhs.x - rhs.x), (sbyte)(lhs.y - rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator -(sbyte2 lhs, sbyte rhs) =>
      new((sbyte)(lhs.x - rhs), (sbyte)(lhs.y - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator -(sbyte lhs, sbyte2 rhs) =>
      new((sbyte)(lhs - rhs.x), (sbyte)(lhs - rhs.y));

    // Unary negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator -(sbyte2 val) => new((sbyte)(-val.x), (sbyte)(-val.y));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator /(sbyte2 lhs, sbyte2 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs.x / rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs.y / rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator /(sbyte2 lhs, sbyte rhs) =>
      rhs == 0 ? zero : new sbyte2((sbyte)(lhs.x / rhs), (sbyte)(lhs.y / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator /(sbyte lhs, sbyte2 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs / rhs.y)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator %(sbyte2 lhs, sbyte2 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs.x % rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs.y % rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator %(sbyte2 lhs, sbyte rhs) =>
      rhs == 0 ? zero : new sbyte2((sbyte)(lhs.x % rhs), (sbyte)(lhs.y % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator %(sbyte lhs, sbyte2 rhs) =>
      new(
        rhs.x == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.x),
        rhs.y == 0 ? (sbyte)0 : (sbyte)(lhs % rhs.y)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator ++(sbyte2 val) => new((sbyte)(val.x + 1), (sbyte)(val.y + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte2 operator --(sbyte2 val) => new((sbyte)(val.x - 1), (sbyte)(val.y - 1));

    // Comparison operators - these return bool2 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(sbyte2 lhs, sbyte2 rhs) => new(lhs.x < rhs.x, lhs.y < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(sbyte2 lhs, sbyte rhs) => new(lhs.x < rhs, lhs.y < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(sbyte lhs, sbyte2 rhs) => new(lhs < rhs.x, lhs < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(sbyte2 lhs, sbyte2 rhs) => new(lhs.x <= rhs.x, lhs.y <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(sbyte2 lhs, sbyte rhs) => new(lhs.x <= rhs, lhs.y <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(sbyte lhs, sbyte2 rhs) => new(lhs <= rhs.x, lhs <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(sbyte2 lhs, sbyte2 rhs) => new(lhs.x > rhs.x, lhs.y > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(sbyte2 lhs, sbyte rhs) => new(lhs.x > rhs, lhs.y > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(sbyte lhs, sbyte2 rhs) => new(lhs > rhs.x, lhs > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(sbyte2 lhs, sbyte2 rhs) => new(lhs.x >= rhs.x, lhs.y >= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(sbyte2 lhs, sbyte rhs) => new(lhs.x >= rhs, lhs.y >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(sbyte lhs, sbyte2 rhs) => new(lhs >= rhs.x, lhs >= rhs.y);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(sbyte2 lhs, sbyte2 rhs) => new(lhs.x == rhs.x, lhs.y == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(sbyte2 lhs, sbyte rhs) => new(lhs.x == rhs, lhs.y == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(sbyte lhs, sbyte2 rhs) => new(lhs == rhs.x, lhs == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(sbyte2 lhs, sbyte2 rhs) => new(lhs.x != rhs.x, lhs.y != rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(sbyte2 lhs, sbyte rhs) => new(lhs.x != rhs, lhs.y != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(sbyte lhs, sbyte2 rhs) => new(lhs != rhs.x, lhs != rhs.y);

    // Swizzling properties
    public readonly sbyte2 xx => new(x, x);
    public readonly sbyte2 xy => new(x, y);
    public readonly sbyte2 yx => new(y, x);
    public readonly sbyte2 yy => new(y, y);

    /// <summary>Returns the sbyte element at a specified index.</summary>
    public unsafe sbyte this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (sbyte2* array = &this)
        {
          return ((sbyte*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (sbyte* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the sbyte2 is equal to a given sbyte2, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(sbyte2 other) => x == other.x && y == other.y;

    /// <summary>Returns true if the sbyte2 is equal to a given sbyte2, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is sbyte2 other && Equals(other);

    /// <summary>Returns a hash code for the sbyte2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y);

    /// <summary>Returns a string representation of the sbyte2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"sbyte2({x}, {y})";

    /// <summary>Returns a string representation of the sbyte2 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"sbyte2({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public sbyte x;
      public sbyte y;

      public DebuggerProxy(sbyte2 v)
      {
        x = v.x;
        y = v.y;
      }
    }
  }
}
