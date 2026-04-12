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
  public struct short2 : IEquatable<short2>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public short x;

    /// <summary>y component of the vector.</summary>
    public short y;

    /// <summary>short2 zero value.</summary>
    public static readonly short2 zero = new(0, 0);

    /// <summary>short2 one value (all components are 1).</summary>
    public static readonly short2 one = new(1, 1);

    /// <summary>short2 maximum value (all components are short.MaxValue).</summary>
    public static readonly short2 max = new(short.MaxValue, short.MaxValue);

    /// <summary>short2 minimum value (all components are short.MinValue).</summary>
    public static readonly short2 min = new(short.MinValue, short.MinValue);

    /// <summary>Constructs a short2 vector from two short values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(short x, short y)
    {
      this.x = x;
      this.y = y;
    }

    /// <summary>Constructs a short2 vector from a single short value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(short v)
    {
      x = v;
      y = v;
    }

    /// <summary>Constructs a short2 vector from a short2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(short2 v)
    {
      x = v.x;
      y = v.y;
    }

    /// <summary>Constructs a short2 vector from a float2 vector by truncating the components to the nearest short value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(float2 v)
    {
      x = (short)(v.x / 1f * short.MaxValue);
      y = (short)(v.y / 1f * short.MaxValue);
    }

    /// <summary>Constructs a short2 vector from a sbyte2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(sbyte2 v)
    {
      x = (short)((float)v.x / sbyte.MaxValue * short.MaxValue);
      y = (short)((float)v.y / sbyte.MaxValue * short.MaxValue);
    }

    /// <summary>Constructs a short2 vector from a single int value by converting it to short and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(int v)
    {
      x = (short)v;
      y = (short)v;
    }

    /// <summary>Constructs a short2 vector from an int2 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short2(int2 v)
    {
      x = (short)v.x;
      y = (short)v.y;
    }

    /// <summary>
    /// Creates a short2 from a float2 in the -1..1 range, mapping to -32768..32767
    /// Useful for high-precision normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 FromNormalized(float2 v) =>
      new() { x = (short)(v.x * short.MaxValue), y = (short)(v.y * short.MaxValue) };

    /// <summary>
    /// Converts a short2 value (-32768..32767) back to normalized float2 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float2 ToNormalized() =>
      new((float)x / short.MaxValue, (float)y / short.MaxValue);

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short2(short v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short2(float2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short2(sbyte2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float2(short2 v) => ToFloat2(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 ToFloat2(short2 s2) =>
      new((float)s2.x / short.MaxValue, (float)s2.y / short.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator short2(int2 v) => new(v);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator *(short2 lhs, short2 rhs) =>
      new((short)(lhs.x * rhs.x), (short)(lhs.y * rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator *(short2 lhs, short rhs) =>
      new((short)(lhs.x * rhs), (short)(lhs.y * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator *(short lhs, short2 rhs) =>
      new((short)(lhs * rhs.x), (short)(lhs * rhs.y));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator +(short2 lhs, short2 rhs) =>
      new((short)(lhs.x + rhs.x), (short)(lhs.y + rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator +(short2 lhs, short rhs) =>
      new((short)(lhs.x + rhs), (short)(lhs.y + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator +(short lhs, short2 rhs) =>
      new((short)(lhs + rhs.x), (short)(lhs + rhs.y));

    // Subtraction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator -(short2 lhs, short2 rhs) =>
      new((short)(lhs.x - rhs.x), (short)(lhs.y - rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator -(short2 lhs, short rhs) =>
      new((short)(lhs.x - rhs), (short)(lhs.y - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator -(short lhs, short2 rhs) =>
      new((short)(lhs - rhs.x), (short)(lhs - rhs.y));

    // Unary negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator -(short2 val) => new((short)(-val.x), (short)(-val.y));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator /(short2 lhs, short2 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs.x / rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs.y / rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator /(short2 lhs, short rhs) =>
      rhs == 0 ? zero : new short2((short)(lhs.x / rhs), (short)(lhs.y / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator /(short lhs, short2 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs / rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs / rhs.y)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator %(short2 lhs, short2 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs.x % rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs.y % rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator %(short2 lhs, short rhs) =>
      rhs == 0 ? zero : new short2((short)(lhs.x % rhs), (short)(lhs.y % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator %(short lhs, short2 rhs) =>
      new(
        rhs.x == 0 ? (short)0 : (short)(lhs % rhs.x),
        rhs.y == 0 ? (short)0 : (short)(lhs % rhs.y)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator ++(short2 val) => new((short)(val.x + 1), (short)(val.y + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short2 operator --(short2 val) => new((short)(val.x - 1), (short)(val.y - 1));

    // Comparison operators - these return bool2 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(short2 lhs, short2 rhs) => new(lhs.x < rhs.x, lhs.y < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(short2 lhs, short rhs) => new(lhs.x < rhs, lhs.y < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(short lhs, short2 rhs) => new(lhs < rhs.x, lhs < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(short2 lhs, short2 rhs) => new(lhs.x <= rhs.x, lhs.y <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(short2 lhs, short rhs) => new(lhs.x <= rhs, lhs.y <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(short lhs, short2 rhs) => new(lhs <= rhs.x, lhs <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(short2 lhs, short2 rhs) => new(lhs.x > rhs.x, lhs.y > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(short2 lhs, short rhs) => new(lhs.x > rhs, lhs.y > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(short lhs, short2 rhs) => new(lhs > rhs.x, lhs > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(short2 lhs, short2 rhs) => new(lhs.x >= rhs.x, lhs.y >= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(short2 lhs, short rhs) => new(lhs.x >= rhs, lhs.y >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(short lhs, short2 rhs) => new(lhs >= rhs.x, lhs >= rhs.y);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(short2 lhs, short2 rhs) => new(lhs.x == rhs.x, lhs.y == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(short2 lhs, short rhs) => new(lhs.x == rhs, lhs.y == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(short lhs, short2 rhs) => new(lhs == rhs.x, lhs == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(short2 lhs, short2 rhs) => new(lhs.x != rhs.x, lhs.y != rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(short2 lhs, short rhs) => new(lhs.x != rhs, lhs.y != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(short lhs, short2 rhs) => new(lhs != rhs.x, lhs != rhs.y);

    // Swizzling properties
    public readonly short2 xx => new(x, x);
    public readonly short2 xy => new(x, y);
    public readonly short2 yx => new(y, x);
    public readonly short2 yy => new(y, y);

    /// <summary>Returns the short element at a specified index.</summary>
    public unsafe short this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (short2* array = &this)
        {
          return ((short*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (short* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the short2 is equal to a given short2, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(short2 other) => x == other.x && y == other.y;

    /// <summary>Returns true if the short2 is equal to a given short2, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is short2 other && Equals(other);

    /// <summary>Returns a hash code for the short2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y);

    /// <summary>Returns a string representation of the short2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"short2({x}, {y})";

    /// <summary>Returns a string representation of the short2 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"short2({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public short x;
      public short y;

      public DebuggerProxy(short2 v)
      {
        x = v.x;
        y = v.y;
      }
    }
  }
}
