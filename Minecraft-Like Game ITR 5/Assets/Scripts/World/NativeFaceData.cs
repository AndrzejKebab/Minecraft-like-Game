using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace PatataGames
{
	[StructLayout(LayoutKind.Sequential)]
	public struct NativeFaceData
	{
		public float3              Normal;
		public NativeArray<Vertex> Vertices;
	}
}