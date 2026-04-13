using System.Runtime.InteropServices;
using Unity.Burst;

namespace _Project
{
	[BurstCompile][StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct Vertex
	{
		public uint Data1; // PosX(10), PosY(10), PosZ(10), AO(2) = 32 bits
		public uint Data2; // Color (32-bit RGBA)
		public uint Data3; // TexBase(9), TexOverlay(9), UV_X(10), FaceIndex(3), Padding(1) = 32 bits
		public uint Data4; // TexNorm(9), TexSpec(9), UV_Y(10), Padding(4) = 32 bits
	}
}