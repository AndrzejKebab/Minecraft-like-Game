using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public static class Utility
{
	public static int FlattenIndex(int x, int y, int z)
	{
		return x | (y << 5) | (z << 10);
	}

	[BurstCompile]
	public static void SetAtIndex<T>(this NativeArray<T> array, int posX, int posY, int posZ, T data) where T : struct
	{
		array[posX | (posY << 5) | (posZ << 10)] = data;
	}

	[BurstCompile]
	public static void UnflattenIndex(in int index, out int3 pos)
	{
		pos = new int3(index & 0x1f, (index >> 5) & 0x1f, (index >> 10) & 0x1f);
	}

	[BurstCompile]
	public static T GetAtPosition<T>(this NativeArray<T> array, int posX, int posY, int posZ) where T : struct
	{
		return array[FlattenIndex(posX, posY, posZ)];
	}

	public static void TryAddComponent<T>(this EntityCommandBuffer ecb, ref SystemState state, Entity entity) where T : unmanaged, IComponentData
	{
		if (!state.EntityManager.HasComponent<T>(entity)) 
			ecb.AddComponent<T>(entity);
	}

	public static Vector3 ToVector3(this ref int3 v)
	{
		return new Vector3(v.x, v.y, v.z);
	}
}