using System.Runtime.InteropServices;
using Unity.Burst;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct Vertex
	{
		public uint Data1; // PosX(10), PosY(10), PosZ(10), UV_X(1), UV_Y(1)
		public uint Data2; // Color (32-bit RGBA)
		public uint Data3; // TexBase(12), TexOverlay(12), NormalIndex(3)
		public uint Data4; // TexNorm(12), TexSpec(12), TangentIndex(3), TangentSign(1)
	}
}