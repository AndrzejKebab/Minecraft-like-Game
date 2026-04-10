using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
	public struct Vertex
	{
		public half4   Position;   // xyz = chunk-local pos, w = unused
		public half4   Normal;     // xyz = normal,          w = unused
		public half4   Tangent;    // xyzw with handedness in w
		public Color32 Color;      // rgba tint
		public half2   UVs;        // uv
		public half4   TextureIDs; // x=base, y=overlay, z=normal, w=specular
	}
}