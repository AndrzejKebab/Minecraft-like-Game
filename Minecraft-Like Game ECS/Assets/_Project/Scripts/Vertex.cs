using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential)]
	public struct Vertex
	{
		public float3 position;
		public float3 normal;
		public float4 Tangent;
		public float3 texCoordBase;
		public float3 texCoordOverlay;
	}
}