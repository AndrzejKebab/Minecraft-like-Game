using System.Runtime.InteropServices;
using Unity.Burst;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct Vertex
	{

		public uint Data1; // Data1: PosX(6), PosY(6), PosZ(6), UV_X(6), UV_Y(6), AO(2) = 32 bits
		public uint Data2; // Color (32-bit RGBA)
		public uint Data3; // TexBase(12), TexOverlay(12), NormalIndex(3)
		public uint Data4; // TexNorm(12), TexSpec(12), TangentIndex(3), TangentSign(1)
	}
}