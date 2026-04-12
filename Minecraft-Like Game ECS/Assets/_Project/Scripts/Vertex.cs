using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
	public struct Vertex
	{
		public float3 PositionWS; // Standard Position (12 bytes)
		public float  Color;      // Packed Color (4 bytes)
		public float  Data3;      // TexBase, Overlay, UV_X, Normal, TanSign (4 bytes)
		public float  Data4;      // TexNorm, Spec, UV_Y, Tangent, AO (4 bytes)
	}
}