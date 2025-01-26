using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace PatataGames
{
	[Serializable, StructLayout(LayoutKind.Sequential)]
	public struct Vertex
	{
		public float3 Position;
		public float2 UV;
	}
}