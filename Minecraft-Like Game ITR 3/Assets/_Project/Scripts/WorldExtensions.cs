using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public static class WorldExtensions
{
	[BurstCompile]
	public static int FlattenIndex(int posX, int posY, int posZ)
		=> posX | (posY << 5) | (posZ << 10);

	[BurstCompile]
	public static void UnflattenIndex(in int index, out int3 pos)
		=> pos = new int3(index & 0x1f, (index >> 5) & 0x1f, (index >> 10) & 0x1f);

	public static Vector3 ToVector3(this ref int3 v) => new(v.x, v.y, v.z);
}