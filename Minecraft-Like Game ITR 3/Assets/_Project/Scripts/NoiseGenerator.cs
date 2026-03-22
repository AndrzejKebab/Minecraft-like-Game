using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public static class NoiseGenerator
{
	[BurstCompile]
	public static void GenerateHeightMap(out NativeTexture2D<float> heightMap,
	                                     ref FastNoise              noise,
	                                     ref int3                   chunkWorldPos,
	                                     int                        chunkSize,
	                                     int                        biomeScale,
	                                     int                        seed)
	{
		var stepSize = 1f / biomeScale;

		heightMap = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.Persistent);
		noise.GenUniformGrid2D(heightMap, out _, chunkWorldPos.x, chunkWorldPos.z, chunkSize,
		                       chunkSize, stepSize, stepSize, seed);
	}

	[BurstCompile]
	public static int HeightFromNoise(float rawNoise, int biomeHeight, int solidGroundHeight)
	{
		return (int)(rawNoise * biomeHeight) + solidGroundHeight;
	}

	[BurstCompile]
	public static ushort ClassifyVoxel(int yPos, int terrainHeight, int solidGroundHeight)
	{
		if (yPos > terrainHeight) return yPos <= solidGroundHeight ? (ushort)5 : (ushort)0; // water or air
		if (yPos == terrainHeight) return 4;                                                // grass
		return yPos > terrainHeight - 6
			       ? (ushort)3
			       : (ushort) // dirt
			       2;         // stone
	}
}