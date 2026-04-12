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
  public struct byte2 : IEquatable<byte2>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public byte x;

    /// <summary>y component of the vector.</summary>
    public byte y;

    /// <summary>byte2 zero value.</summary>
    public static readonly byte2 zero = new(0, 0);

    /// <summary>byte2 one value (all components are 1).</summary>
    public static readonly byte2 one = new(1, 1);

    /// <summary>byte2 maximum value (all components are byte.MaxValue).</summary>
    public static readonly byte2 max = new(byte.MaxValue, byte.MaxValue);

    /// <summary>Constructs a byte2 vector from two byte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(byte x, byte y)
    {
      this.x = x;
      this.y = y;
    }

    /// <summary>Constructs a byte2 vector from a single byte value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(byte v)
    {
      x = v;
      y = v;
    }

    /// <summary>Constructs a byte2 vector from a byte2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(byte2 v)
    {
      x = v.x;
      y = v.y;
    }

    /// <summary>Constructs a byte2 vector from a float2 vector by truncating the components to the nearest byte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(float2 v)
    {
      x = (byte)(v.x / 1f * byte.MaxValue);
      y = (byte)(v.y / 1f * byte.MaxValue);
    }

    /// <summary>Constructs a byte2 vector from a ushort2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(ushort2 v)
    {
      x = (byte)((float)v.x / ushort.MaxValue * byte.MaxValue);
      y = (byte)((float)v.y / ushort.MaxValue * byte.MaxValue);
    }

    /// <summary>Constructs a byte2 vector from a single int value by converting it to byte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(int v)
    {
      x = (byte)v;
      y = (byte)v;
    }

    /// <summary>Constructs a byte2 vector from an int2 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(int2 v)
    {
      x = (byte)v.x;
      y = (byte)v.y;
    }

    /// <summary>Constructs a byte2 vector from a single uint value by converting it to byte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(uint v)
    {
      x = (byte)v;
      y = (byte)v;
    }

    /// <summary>Constructs a byte2 vector from a uint2 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte2(uint2 v)
    {
      x = (byte)v.x;
      y = (byte)v.y;
    }

    /// <summary>
    /// Creates a byte2 from a float2 in the -1..1 range, mapping to 0..255
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 FromNormalized(float2 v) =>
      new()
      {
        x = (byte)(((v.x * 0.5f) + 0.5f) * byte.MaxValue),
        y = (byte)(((v.y * 0.5f) + 0.5f) * byte.MaxValue),
      };

    /// <summary>
    /// Converts a byte2 value (0..255) back to normalized float2 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float2 ToNormalized() =>
      new(((float)x / byte.MaxValue * 2f) - 1f, ((float)y / byte.MaxValue * 2f) - 1f);

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte2(byte v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte2(float2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte2(ushort2 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float2(byte2 v) => ToFloat2(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 ToFloat2(byte2 b2) =>
      new((float)b2.x / byte.MaxValue, (float)b2.y / byte.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte2(int2 v) => new((byte)v.x, (byte)v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte2(uint2 v) => new((byte)v.x, (byte)v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte2(byte3 v) => new(v.x, v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte2(byte4 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator *(byte2 lhs, byte2 rhs) =>
      new((byte)(lhs.x * rhs.x), (byte)(lhs.y * rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator *(byte2 lhs, byte rhs) =>
      new((byte)(lhs.x * rhs), (byte)(lhs.y * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator *(byte lhs, byte2 rhs) =>
      new((byte)(lhs * rhs.x), (byte)(lhs * rhs.y));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator +(byte2 lhs, byte2 rhs) =>
      new((byte)(lhs.x + rhs.x), (byte)(lhs.y + rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator +(byte2 lhs, byte rhs) =>
      new((byte)(lhs.x + rhs), (byte)(lhs.y + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator +(byte lhs, byte2 rhs) =>
      new((byte)(lhs + rhs.x), (byte)(lhs + rhs.y));

    // Subtraction (with clamping to zero)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator -(byte2 lhs, byte2 rhs) =>
      new((byte)Math.Max(0, lhs.x - rhs.x), (byte)Math.Max(0, lhs.y - rhs.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator -(byte2 lhs, byte rhs) =>
      new((byte)Math.Max(0, lhs.x - rhs), (byte)Math.Max(0, lhs.y - rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator -(byte lhs, byte2 rhs) =>
      new((byte)Math.Max(0, lhs - rhs.x), (byte)Math.Max(0, lhs - rhs.y));

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator /(byte2 lhs, byte2 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs.x / rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs.y / rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator /(byte2 lhs, byte rhs) =>
      rhs == 0 ? zero : new byte2((byte)(lhs.x / rhs), (byte)(lhs.y / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator /(byte lhs, byte2 rhs) =>
      new(rhs.x == 0 ? (byte)0 : (byte)(lhs / rhs.x), rhs.y == 0 ? (byte)0 : (byte)(lhs / rhs.y));

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator %(byte2 lhs, byte2 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs.x % rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs.y % rhs.y)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator %(byte2 lhs, byte rhs) =>
      rhs == 0 ? zero : new byte2((byte)(lhs.x % rhs), (byte)(lhs.y % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator %(byte lhs, byte2 rhs) =>
      new(rhs.x == 0 ? (byte)0 : (byte)(lhs % rhs.x), rhs.y == 0 ? (byte)0 : (byte)(lhs % rhs.y));

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator ++(byte2 val) => new((byte)(val.x + 1), (byte)(val.y + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte2 operator --(byte2 val) =>
      new((byte)Math.Max(0, val.x - 1), (byte)Math.Max(0, val.y - 1));

    // Comparison operators - these return bool2 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(byte2 lhs, byte2 rhs) => new(lhs.x < rhs.x, lhs.y < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(byte2 lhs, byte rhs) => new(lhs.x < rhs, lhs.y < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <(byte lhs, byte2 rhs) => new(lhs < rhs.x, lhs < rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(byte2 lhs, byte2 rhs) => new(lhs.x <= rhs.x, lhs.y <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(byte2 lhs, byte rhs) => new(lhs.x <= rhs, lhs.y <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator <=(byte lhs, byte2 rhs) => new(lhs <= rhs.x, lhs <= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(byte2 lhs, byte2 rhs) => new(lhs.x > rhs.x, lhs.y > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(byte2 lhs, byte rhs) => new(lhs.x > rhs, lhs.y > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >(byte lhs, byte2 rhs) => new(lhs > rhs.x, lhs > rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(byte2 lhs, byte2 rhs) => new(lhs.x >= rhs.x, lhs.y >= rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(byte2 lhs, byte rhs) => new(lhs.x >= rhs, lhs.y >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator >=(byte lhs, byte2 rhs) => new(lhs >= rhs.x, lhs >= rhs.y);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(byte2 lhs, byte2 rhs) => new(lhs.x == rhs.x, lhs.y == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(byte2 lhs, byte rhs) => new(lhs.x == rhs, lhs.y == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator ==(byte lhs, byte2 rhs) => new(lhs == rhs.x, lhs == rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(byte2 lhs, byte2 rhs) => new(lhs.x != rhs.x, lhs.y != rhs.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(byte2 lhs, byte rhs) => new(lhs.x != rhs, lhs.y != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool2 operator !=(byte lhs, byte2 rhs) => new(lhs != rhs.x, lhs != rhs.y);

    // Swizzling properties
    public readonly byte2 xx => new(x, x);
    public readonly byte2 xy => new(x, y);
    public readonly byte2 yx => new(y, x);
    public readonly byte2 yy => new(y, y);

    /// <summary>Returns the byte element at a specified index.</summary>
    public unsafe byte this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (byte2* array = &this)
        {
          return ((byte*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 2)
          throw new ArgumentException("index must be between[0...1]");
#endif
        fixed (byte* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the byte2 is equal to a given byte2, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(byte2 other) => x == other.x && y == other.y;

    /// <summary>Returns true if the byte2 is equal to a given byte2, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is byte2 other && Equals(other);

    /// <summary>Returns a hash code for the byte2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y);

    /// <summary>Returns a string representation of the byte2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"byte2({x}, {y})";

    /// <summary>Returns a string representation of the byte2 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"byte2({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public byte x;
      public byte y;

      public DebuggerProxy(byte2 v)
      {
        x = v.x;
        y = v.y;
      }
    }
  }
}
