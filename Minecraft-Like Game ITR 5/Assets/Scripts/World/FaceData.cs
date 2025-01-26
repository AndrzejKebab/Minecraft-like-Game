using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace PatataGames
{
	[Serializable, StructLayout(LayoutKind.Sequential)]
	public struct FaceData
	{
		public float3 Normal;
		public Vertex[] Vertices;
	}
}