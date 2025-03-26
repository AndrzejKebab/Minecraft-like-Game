using System;
using Unity.Burst;
using Unity.Collections;

namespace PatataGames;

[BurstCompile]
public struct MeshDataNative : IDisposable
{
	public NativeArray<FaceDataNative> FaceDatas;

	public MeshDataNative(MeshData meshData)
	{
		FaceDatas = new NativeArray<FaceDataNative>(meshData.FaceDatas.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
		for (int i = 0; i < meshData.FaceDatas.Length; i++)
		{
			FaceDatas[i] = new FaceDataNative(meshData.FaceDatas[i]);
		}
	}

	public void Dispose()
	{
		FaceDatas.Dispose();
	}
}