using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public static class NoiseGenerator
	{
		[BurstCompile]
		public static void GenerateHeightMap(out NativeTexture2D<float> heightMap,
		                                     ref FastNoise              noise,
		                                     ref int3                   chunkWorldPos,
		                                     int                        chunkSize,
		                                     int                        seed)
		{
			heightMap = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.Persistent);
			noise.GenUniformGrid2D(heightMap, out _, chunkWorldPos.x, chunkWorldPos.z, chunkSize,
			                       chunkSize, 1, 1, seed);
		}

		[BurstCompile]
		public static int HeightFromNoise(float rawNoise, int biomeHeight)
		{
			return (int)(rawNoise * biomeHeight);
		}

		[BurstCompile]
		public static ushort ClassifyVoxel(int yPos, int terrainHeight)
		{
			return yPos switch
			       {
				       _ when yPos > terrainHeight && yPos <= 0  => 4, // water
				       _ when yPos == terrainHeight && yPos <= 0 => 5, // sand
				       _ when yPos > terrainHeight               => 0, // air
				       _ when yPos == terrainHeight              => 3, // grass
				       _ when yPos > terrainHeight - 6           => 2, // dirt
				       _                                         => 1  // stone
			       };
		}
	}
}