using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public static class Utility
{
	[BurstCompile]
	public static void FlattenIndex(this int index, int posX, int posY, int posZ)
	{
		index = posX | (posY << 5) | (posZ << 10);
	}

	[BurstCompile]
	public static void SetAtIndex<T>(this NativeArray<T> array, int posX, int posY, int posZ, T data) where T : struct
	{
		var index = posX | (posY << 5) | (posZ << 10);
		array[index] = data;
	}

	[BurstCompile]
	public static void UnflattenIndex(in int index, out int3 pos)
	{
		pos = new int3(index & 0x1f, (index >> 5) & 0x1f, (index >> 10) & 0x1f);
	}

	[BurstCompile]
	public static T GetAtPosition<T>(this NativeArray<T> array, int posX, int posY, int posZ) where T : struct
	{
		var index = 0;
		index.FlattenIndex(posX, posY, posZ);
		return array[index];
	}

	public static Vector3 ToVector3(this ref int3 v)
	{
		return new Vector3(v.x, v.y, v.z);
	}
}