using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	// manageable struct for use in SO to beable set it in inspector
	[Serializable]
	public struct VoxelMeshData
	{
		public float3[] Vertices;
		public int4[]   Triangles; // Stores 4 vertex indices to represent a quad
	}

// Native version for runtime use
	[BurstCompile]
	public struct NativeVoxelMeshData
	{
		public NativeArray<float3> Vertices;
		public NativeArray<int4>   Triangles;
	}
}