using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable 0659
[Serializable]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
[StructLayout(LayoutKind.Sequential, Size = 4, Pack = 1)]
public struct sbyte4(sbyte x, sbyte y, sbyte z, sbyte w)
	: IEquatable<sbyte4>, IFormattable
{
	public sbyte x = x, y = y, z = z, w = w;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(sbyte4 rhs)
	{
		return x == rhs.x && y == rhs.y && z == rhs.z && w == rhs.w;
	}

	public override bool Equals(object o)
	{
		return o is sbyte4 c && Equals(c);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString()
	{
		return $"sbyte4({x}, {y}, {z}, {w})";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string ToString(string format, IFormatProvider formatProvider)
	{
		return
			$"sbyte4({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)}, {w.ToString(format, formatProvider)})";
	}
}