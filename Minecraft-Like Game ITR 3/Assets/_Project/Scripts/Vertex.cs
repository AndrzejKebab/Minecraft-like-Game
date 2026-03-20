using System.Runtime.InteropServices;
using NativeTexture.Formats;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential, Size = 20, Pack = 1)]
public struct Vertex(half4 position, sbyte4 normal, sbyte4 color, half2 uv)
{
	public half4  Position = position; // 8 bytes
	public sbyte4 Normal   = normal;   // 4 bytes
	public sbyte4 Color    = color;    // 4 bytes
	public half2  UVs      = uv;       // 4 bytes
}