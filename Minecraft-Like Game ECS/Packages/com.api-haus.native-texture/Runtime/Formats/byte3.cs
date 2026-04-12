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
  public struct byte3 : IEquatable<byte3>
#pragma warning restore IDE1006 // Naming Styles
  {
    /// <summary>x component of the vector.</summary>
    public byte x;

    /// <summary>y component of the vector.</summary>
    public byte y;

    /// <summary>z component of the vector.</summary>
    public byte z;

    /// <summary>byte3 zero value.</summary>
    public static readonly byte3 zero = new(0, 0, 0);

    /// <summary>byte3 one value (all components are 1).</summary>
    public static readonly byte3 one = new(1, 1, 1);

    /// <summary>byte3 maximum value (all components are byte.MaxValue).</summary>
    public static readonly byte3 max = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>Constructs a byte3 vector from three byte values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(byte x, byte y, byte z)
    {
      this.x = x;
      this.y = y;
      this.z = z;
    }

    /// <summary>Constructs a byte3 vector from a single byte value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(byte v)
    {
      x = v;
      y = v;
      z = v;
    }

    /// <summary>Constructs a byte3 vector from a byte3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(byte3 v)
    {
      x = v.x;
      y = v.y;
      z = v.z;
    }

    /// <summary>Constructs a byte3 vector from a byte2 vector and a byte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(byte2 xy, byte z)
    {
      x = xy.x;
      y = xy.y;
      this.z = z;
    }

    /// <summary>Constructs a byte3 vector from a byte value and a byte2 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(byte x, byte2 yz)
    {
      this.x = x;
      y = yz.x;
      z = yz.y;
    }

    /// <summary>Constructs a byte3 vector from a float3 vector by truncating the components to the nearest byte value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(float3 v)
    {
      x = (byte)(v.x / 1f * byte.MaxValue);
      y = (byte)(v.y / 1f * byte.MaxValue);
      z = (byte)(v.z / 1f * byte.MaxValue);
    }

    /// <summary>Constructs a byte3 vector from a ushort3 vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(ushort3 v)
    {
      x = (byte)((float)v.x / ushort.MaxValue * byte.MaxValue);
      y = (byte)((float)v.y / ushort.MaxValue * byte.MaxValue);
      z = (byte)((float)v.z / ushort.MaxValue * byte.MaxValue);
    }

    /// <summary>Constructs a byte3 vector from a single int value by converting it to byte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(int v)
    {
      x = (byte)v;
      y = (byte)v;
      z = (byte)v;
    }

    /// <summary>Constructs a byte3 vector from an int3 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(int3 v)
    {
      x = (byte)v.x;
      y = (byte)v.y;
      z = (byte)v.z;
    }

    /// <summary>Constructs a byte3 vector from a single uint value by converting it to byte and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(uint v)
    {
      x = (byte)v;
      y = (byte)v;
      z = (byte)v;
    }

    /// <summary>Constructs a byte3 vector from a uint3 vector by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte3(uint3 v)
    {
      x = (byte)v.x;
      y = (byte)v.y;
      z = (byte)v.z;
    }

    /// <summary>Extracts the xy components from a byte3 vector to create a byte2.</summary>
    public readonly byte2 xy
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => new(x, y);
    }

    /// <summary>Extracts the xy components (explicitly) from a byte3 vector to create a byte2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte2 GetXY() => new(x, y);

    /// <summary>
    /// Creates a byte3 from a float3 in the -1..1 range, mapping to 0..255
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 FromNormalized(float3 v) =>
      new()
      {
        x = (byte)(((v.x * 0.5f) + 0.5f) * byte.MaxValue),
        y = (byte)(((v.y * 0.5f) + 0.5f) * byte.MaxValue),
        z = (byte)(((v.z * 0.5f) + 0.5f) * byte.MaxValue),
      };

    /// <summary>
    /// Converts a byte3 value (0..255) back to normalized float3 (-1..1)
    /// Useful for normal map data
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float3 ToNormalized() =>
      new(
        ((float)x / byte.MaxValue * 2f) - 1f,
        ((float)y / byte.MaxValue * 2f) - 1f,
        ((float)z / byte.MaxValue * 2f) - 1f
      );

    // Implicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte3(byte v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte3(float3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte3(ushort3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float3(byte3 v) => ToFloat3(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float3 ToFloat3(byte3 b3) =>
      new((float)b3.x / byte.MaxValue, (float)b3.y / byte.MaxValue, (float)b3.z / byte.MaxValue);

    // Explicit conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte3(int3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte3(uint3 v) => new(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte3(byte4 v) => new(v.x, v.y, v.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator byte2(byte3 v) => new(v.x, v.y);

    // Operators
    // Multiplication
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator *(byte3 lhs, byte3 rhs) =>
      new((byte)(lhs.x * rhs.x), (byte)(lhs.y * rhs.y), (byte)(lhs.z * rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator *(byte3 lhs, byte rhs) =>
      new((byte)(lhs.x * rhs), (byte)(lhs.y * rhs), (byte)(lhs.z * rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator *(byte lhs, byte3 rhs) =>
      new((byte)(lhs * rhs.x), (byte)(lhs * rhs.y), (byte)(lhs * rhs.z));

    // Addition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator +(byte3 lhs, byte3 rhs) =>
      new((byte)(lhs.x + rhs.x), (byte)(lhs.y + rhs.y), (byte)(lhs.z + rhs.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator +(byte3 lhs, byte rhs) =>
      new((byte)(lhs.x + rhs), (byte)(lhs.y + rhs), (byte)(lhs.z + rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator +(byte lhs, byte3 rhs) =>
      new((byte)(lhs + rhs.x), (byte)(lhs + rhs.y), (byte)(lhs + rhs.z));

    // Subtraction (with clamping to zero)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator -(byte3 lhs, byte3 rhs) =>
      new(
        (byte)Math.Max(0, lhs.x - rhs.x),
        (byte)Math.Max(0, lhs.y - rhs.y),
        (byte)Math.Max(0, lhs.z - rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator -(byte3 lhs, byte rhs) =>
      new(
        (byte)Math.Max(0, lhs.x - rhs),
        (byte)Math.Max(0, lhs.y - rhs),
        (byte)Math.Max(0, lhs.z - rhs)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator -(byte lhs, byte3 rhs) =>
      new(
        (byte)Math.Max(0, lhs - rhs.x),
        (byte)Math.Max(0, lhs - rhs.y),
        (byte)Math.Max(0, lhs - rhs.z)
      );

    // Division
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator /(byte3 lhs, byte3 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs.x / rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs.y / rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs.z / rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator /(byte3 lhs, byte rhs) =>
      rhs == 0 ? zero : new byte3((byte)(lhs.x / rhs), (byte)(lhs.y / rhs), (byte)(lhs.z / rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator /(byte lhs, byte3 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs / rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs / rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs / rhs.z)
      );

    // Modulo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator %(byte3 lhs, byte3 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs.x % rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs.y % rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs.z % rhs.z)
      );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator %(byte3 lhs, byte rhs) =>
      rhs == 0 ? zero : new byte3((byte)(lhs.x % rhs), (byte)(lhs.y % rhs), (byte)(lhs.z % rhs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator %(byte lhs, byte3 rhs) =>
      new(
        rhs.x == 0 ? (byte)0 : (byte)(lhs % rhs.x),
        rhs.y == 0 ? (byte)0 : (byte)(lhs % rhs.y),
        rhs.z == 0 ? (byte)0 : (byte)(lhs % rhs.z)
      );

    // Increment and decrement
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator ++(byte3 val) =>
      new((byte)(val.x + 1), (byte)(val.y + 1), (byte)(val.z + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte3 operator --(byte3 val) =>
      new((byte)Math.Max(0, val.x - 1), (byte)Math.Max(0, val.y - 1), (byte)Math.Max(0, val.z - 1));

    // Comparison operators - these return bool3 from Unity.Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(byte3 lhs, byte3 rhs) =>
      new(lhs.x < rhs.x, lhs.y < rhs.y, lhs.z < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(byte3 lhs, byte rhs) =>
      new(lhs.x < rhs, lhs.y < rhs, lhs.z < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <(byte lhs, byte3 rhs) =>
      new(lhs < rhs.x, lhs < rhs.y, lhs < rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(byte3 lhs, byte3 rhs) =>
      new(lhs.x <= rhs.x, lhs.y <= rhs.y, lhs.z <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(byte3 lhs, byte rhs) =>
      new(lhs.x <= rhs, lhs.y <= rhs, lhs.z <= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator <=(byte lhs, byte3 rhs) =>
      new(lhs <= rhs.x, lhs <= rhs.y, lhs <= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(byte3 lhs, byte3 rhs) =>
      new(lhs.x > rhs.x, lhs.y > rhs.y, lhs.z > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(byte3 lhs, byte rhs) =>
      new(lhs.x > rhs, lhs.y > rhs, lhs.z > rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >(byte lhs, byte3 rhs) =>
      new(lhs > rhs.x, lhs > rhs.y, lhs > rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(byte3 lhs, byte3 rhs) =>
      new(lhs.x >= rhs.x, lhs.y >= rhs.y, lhs.z >= rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(byte3 lhs, byte rhs) =>
      new(lhs.x >= rhs, lhs.y >= rhs, lhs.z >= rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator >=(byte lhs, byte3 rhs) =>
      new(lhs >= rhs.x, lhs >= rhs.y, lhs >= rhs.z);

    // Equality operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(byte3 lhs, byte3 rhs) =>
      new(lhs.x == rhs.x, lhs.y == rhs.y, lhs.z == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(byte3 lhs, byte rhs) =>
      new(lhs.x == rhs, lhs.y == rhs, lhs.z == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator ==(byte lhs, byte3 rhs) =>
      new(lhs == rhs.x, lhs == rhs.y, lhs == rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(byte3 lhs, byte3 rhs) =>
      new(lhs.x != rhs.x, lhs.y != rhs.y, lhs.z != rhs.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(byte3 lhs, byte rhs) =>
      new(lhs.x != rhs, lhs.y != rhs, lhs.z != rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool3 operator !=(byte lhs, byte3 rhs) =>
      new(lhs != rhs.x, lhs != rhs.y, lhs != rhs.z);

    // Swizzling properties
    public readonly byte2 xx => new(x, x);

    /* xy property is defined earlier as a standard component accessor */
    public readonly byte2 xz => new(x, z);

    public readonly byte2 yx => new(y, x);

    public readonly byte2 yy => new(y, y);

    public readonly byte2 yz => new(y, z);

    public readonly byte2 zx => new(z, x);

    public readonly byte2 zy => new(z, y);

    public readonly byte2 zz => new(z, z);

    // 3-component swizzles
    public readonly byte3 xxx => new(x, x, x);
    public readonly byte3 xxy => new(x, x, y);
    public readonly byte3 xxz => new(x, x, z);
    public readonly byte3 xyx => new(x, y, x);
    public readonly byte3 xyy => new(x, y, y);
    public readonly byte3 xyz => new(x, y, z);
    public readonly byte3 xzx => new(x, z, x);
    public readonly byte3 xzy => new(x, z, y);
    public readonly byte3 xzz => new(x, z, z);

    public readonly byte3 yxx => new(y, x, x);
    public readonly byte3 yxy => new(y, x, y);
    public readonly byte3 yxz => new(y, x, z);
    public readonly byte3 yyx => new(y, y, x);
    public readonly byte3 yyy => new(y, y, y);
    public readonly byte3 yyz => new(y, y, z);
    public readonly byte3 yzx => new(y, z, x);
    public readonly byte3 yzy => new(y, z, y);
    public readonly byte3 yzz => new(y, z, z);

    public readonly byte3 zxx => new(z, x, x);
    public readonly byte3 zxy => new(z, x, y);
    public readonly byte3 zxz => new(z, x, z);
    public readonly byte3 zyx => new(z, y, x);
    public readonly byte3 zyy => new(z, y, y);
    public readonly byte3 zyz => new(z, y, z);
    public readonly byte3 zzx => new(z, z, x);
    public readonly byte3 zzy => new(z, z, y);
    public readonly byte3 zzz => new(z, z, z);

    /// <summary>Returns the byte element at a specified index.</summary>
    public unsafe byte this[int index]
    {
      get
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (byte3* array = &this)
        {
          return ((byte*)array)[index];
        }
      }
      set
      {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new ArgumentException("index must be between[0...2]");
#endif
        fixed (byte* array = &x)
        {
          array[index] = value;
        }
      }
    }

    /// <summary>Returns true if the byte3 is equal to a given byte3, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(byte3 other) => x == other.x && y == other.y && z == other.z;

    /// <summary>Returns true if the byte3 is equal to a given byte3, false otherwise.</summary>
    public readonly override bool Equals(object obj) => obj is byte3 other && Equals(other);

    /// <summary>Returns a hash code for the byte3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z);

    /// <summary>Returns a string representation of the byte3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly string ToString() => $"byte3({x}, {y}, {z})";

    /// <summary>Returns a string representation of the byte3 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly string ToString(string format, IFormatProvider formatProvider) =>
      $"byte3({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)})";

    internal sealed class DebuggerProxy
    {
      public byte x;
      public byte y;
      public byte z;

      public DebuggerProxy(byte3 v)
      {
        x = v.x;
        y = v.y;
        z = v.z;
      }
    }
  }
}
