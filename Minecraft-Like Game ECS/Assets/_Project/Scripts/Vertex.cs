using System.Runtime.InteropServices;
using NativeTexture.Formats;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
	public struct Vertex
	{
		public half4   Position; // xyz = pos,     w = unused
		public half4   Normal;   // xyz = normal,  w = unused
		public half4   Tangent;  // xyzw with handedness in w
		public Color32 Color;    // rgba vertex color
		public half2   UVs;      // xy = uv, z = unused, w = unused
		public half4   TexturesIDs; // xyzw = 4 texture IDs for the vertex, used for texture arrays
	}
}