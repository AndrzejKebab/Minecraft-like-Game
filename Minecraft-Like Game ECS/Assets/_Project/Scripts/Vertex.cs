using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Size = 40, Pack = 1)] // (5 properties * 8 bytes)
	public struct Vertex
	{
		public half4 Position; // xyz = pos,     w = unused
		public half4 Normal;   // xyz = normal,  w = unused
		public half4 Tangent;  // xyzw with handedness in w
		public half4 UVs;      // xy = uv, z = texBase, w = texOverlay
		public half4 Color;    // rgba vertex color
	}
}