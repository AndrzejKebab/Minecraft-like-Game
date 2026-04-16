using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public static class Utility
	{
		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FlattenIndex([NoAlias]int x, [NoAlias]int y, [NoAlias]int z)
		{
			return x | (y << 5) | (z << 10);
		}

		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAtIndex<T>(this NativeArray<T> array, [NoAlias]int posX, [NoAlias]int posY, [NoAlias]int posZ, T data) where T : struct
		{
			array[posX | (posY << 5) | (posZ << 10)] = data;
		}

		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void UnflattenIndex(in int index, out int3 pos)
		{
			pos = new int3(index & 0x1f, (index >> 5) & 0x1f, (index >> 10) & 0x1f);
		}

		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T GetAtPosition<T>(this NativeArray<T> array, [NoAlias]int posX, [NoAlias]int posY, [NoAlias]int posZ) where T : struct
		{
			return array[FlattenIndex(posX, posY, posZ)];
		}
	
		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TryAddComponent<T>(this EntityCommandBuffer ecb, ref SystemState state, Entity entity) where T : unmanaged, IComponentData
		{
			if (!state.EntityManager.HasComponent<T>(entity)) 
				ecb.AddComponent<T>(entity);
		}	
	
		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TryAddComponent<T>(this EntityCommandBuffer ecb, ref SystemState state, Entity entity, T component) where T : unmanaged, IComponentData
		{
			if (!state.EntityManager.HasComponent<T>(entity)) 
				ecb.AddComponent(entity, component);
		}
	
		[BurstCompile]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TryRemoveComponent<T>(this EntityCommandBuffer ecb, ref SystemState state, Entity entity) where T : unmanaged, IComponentData
		{
			if (state.EntityManager.HasComponent<T>(entity)) 
				ecb.RemoveComponent<T>(entity);
		}
		
		public static Vector3 ToVector3(this ref int3 v)
		{
			return new Vector3(v.x, v.y, v.z);
		}
	}
}