using System.Runtime.InteropServices;
using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using static FastNoise;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class NoiseGenerator : MonoBehaviour
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public static float Get2DPerlin(float positionX, float positionY, float scale)
	{
		var sampleX = positionX / scale;
		var sampleY = positionY / scale;

		var sampleXY = new float2(sampleX, sampleY);

		var value = noise.cnoise(sampleXY);

		return math.unlerp(-1, 1, value);
	}

	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public unsafe static float Get2DPerlin(IntPtr nodePtr, float positionX, float positionY, float scale)
	{
		var sampleX = positionX / scale;
		var sampleY = positionY / scale;
		return GenSingle2D(nodePtr, sampleX, sampleY, 1337);
	}
}