using System.Runtime.InteropServices;
using Unity.Burst;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct Vertex
	{
		public uint Data1; // PosX(10), PosY(10), PosZ(10), AO(2) = 32 bits
		public uint Data2; // Color (32-bit RGBA)
		public uint Data3; // TexBase(9), TexOverlay(9), TexNorm(9), FaceIndex(3), Padding(2) = 32 bits
		public uint Data4; // TexSpec(9), Padding(23) = 32 bits
	}
}