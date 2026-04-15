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
		public static void GenerateTerrainMap(out NativeTexture2D<int> heightMap,
		                                     ref  FastNoise            continentalnessNoise,
		                                     ref  FastNoise            peaksAndValleysNoise,
		                                     ref  FastNoise            erosionNoise,
		                                     ref  int3                 chunkWorldPos,
		                                     int                       chunkSize,
		                                     int                       seed,
		                                     ref NativeCurve           continentalnessHeight,
		                                     ref NativeCurve            erosionCurve,
		                                     ref NativeCurve           peaksAndValleysCurve
		                                     )
		{
			heightMap = new NativeTexture2D<int>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			var continentalness = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			var erosionMap =  new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			var peaksAndValleysMap =  new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			
			continentalnessNoise.GenUniformGrid2D(continentalness, out _, chunkWorldPos.x, chunkWorldPos.z, chunkSize,
			                                      chunkSize, 1, 1, seed);
			peaksAndValleysNoise.GenUniformGrid2D(erosionMap, out _, chunkWorldPos.x, chunkWorldPos.z, chunkSize,
			                                      chunkSize, 1, 1, seed);
			erosionNoise.GenUniformGrid2D(peaksAndValleysMap, out _, chunkWorldPos.x, chunkWorldPos.z, chunkSize,
			                              chunkSize, 1, 1, seed);

			for (var x = 0; x < heightMap.Height; x++)
			{
				for(var z = 0; z < heightMap.Width; z++)
				{
					heightMap[x, z] = HeightFromNoise(continentalness[x, z], erosionMap[x, z], peaksAndValleysMap[x, z], ref continentalnessHeight, ref peaksAndValleysCurve, ref erosionCurve);
				}
			}
		}
		
		[BurstCompile]
		public static void GenerateCaveMap(out NativeTexture3D<float> caveMap, ref FastNoise caveNoise, ref int3 chunkWorldPos, int chunkSize, int seed)
		{
			caveMap = new NativeTexture3D<float>(new int3(chunkSize, chunkSize, chunkSize), Allocator.TempJob);
			caveNoise.GenUniformGrid3D(caveMap,  out _ , chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z, chunkSize, chunkSize, chunkSize, 1,1,1, seed);
		}
		
		[BurstCompile]
		private static int HeightFromNoise(float continentalnessMap, float peaksAndValleysMap, float erosionMap, ref NativeCurve biomeHeight, ref NativeCurve erosionCurve,
		                                  ref NativeCurve peaksAndValleysCurve)
		{
			var baseHeight      = biomeHeight.Evaluate(continentalnessMap);
			var peaksAndValleys = peaksAndValleysCurve.Evaluate(peaksAndValleysMap);
			var erosion         = erosionCurve.Evaluate(erosionMap);
			
			return (int)math.mad(erosion * 0.4f, peaksAndValleys * .66f, baseHeight);
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