using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PatataGames;

[BurstCompile]
public struct FaceDataNative : IDisposable
{
	public NativeArray<Vertex> Vertices;
	public float3        Normal;

	public FaceDataNative(FaceData faceData)
	{
		Vertices = new NativeArray<Vertex>(faceData.Vertices.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
		for (var i = 0; i < faceData.Vertices.Length; i++)
		{
			Vertices[i] = faceData.Vertices[i];
		}
		Normal = faceData.Normal;
	}


	public void Dispose()
	{
		Vertices.Dispose();
	}
}