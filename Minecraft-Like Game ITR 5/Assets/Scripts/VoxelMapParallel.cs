using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static PatataStudio.GameSettings;

namespace PatataStudio
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public struct VoxelMapParallel : IJobParallelFor
	{
		[ReadOnly]
		[NativeDisableUnsafePtrRestriction]
		public IntPtr NoiseNodeTreePtr;
		[ReadOnly]
		public NativeList<int3> ChunksToUpdate;
		[WriteOnly]
		public NativeParallelHashMap<int3, Chunk>.ParallelWriter ChunkMap;

		public void Execute(int index)
		{
			ChunkMap.TryAdd(ChunksToUpdate[index], GenerateVoxelMap(ChunksToUpdate[index]));
		}

		private Chunk GenerateVoxelMap(int3 chunkPos)
		{
			var chunk = new Chunk(chunkPos, 128, Allocator.Persistent);

			var noiseValues = GenerateHeightNoise(chunkPos);

			var blockIdList = new NativeList<int>(Allocator.Temp);

			var currentBlockId = GetBlock(noiseValues[0], chunkPos.y);
			var count = 0;

			for (int i = 0; i < noiseValues.Length; i++)
			{
				int posY = (i / ChunkSize) % ChunkSize + chunkPos.y;
				var blockId = GetBlock(noiseValues[i], posY);

				if(blockId == currentBlockId)
				{
					count++;
				}
				else
				{
					chunk.VoxelMap.AddInterval(currentBlockId, count);
					currentBlockId = blockId;
					count = 1;
				}
			}

			chunk.VoxelMap.AddInterval(currentBlockId, count);

			return chunk;
		}

		private int GetBlock(float noiseValue, int yPos)
		{
			// calculate terrain height and subtract MaxTerrainHeight / 2 so noiseValue of 0.5 will be at Y = 0 (a sea level)
			int terrainHeight = (int)math.floor((noiseValue * MaxTerrainHeight) - (MaxTerrainHeight * 0.5f));
			
			var voxelID = 0;

			HeightPass(out voxelID, terrainHeight, yPos);
			CavePass();

			return voxelID;
		}

		private void HeightPass(out int voxelID, int terrainHeight, int yPos)
		{
			voxelID = 1; // default block is Stone

			if (yPos > terrainHeight)
			{
				voxelID = yPos <= 0 ? 4 : 0; // if yPos is greater than the terrainHeight and yPos is less or equal 0, use water else air
			}
			else if (yPos == terrainHeight)
			{
				voxelID = 3; // if yPos is equal to the terrainHeight, use grass
			}
			else if (yPos < terrainHeight && yPos > terrainHeight - 6)
			{
				voxelID = 2; // if yPos is less than the terrainHeight and yPos is greater than 6, use dirt
			}
		}

		private void CavePass()
		{

		}

		private void BiomePass()
		{

		}

		private void DecorationPass()
		{

		}


		private NativeArray<float> GenerateHeightNoise(int3 chunkPos)
		{
			NativeArray<float> noiseOut = new((int)math.pow(ChunkSize, 2), Allocator.Temp);
			NativeArray<float> continentalness = new((int)math.pow(ChunkSize, 2), Allocator.Temp);
			NativeArray<float> erosion = new((int)math.pow(ChunkSize, 2), Allocator.Temp);
			NativeArray<float> peaksandvalleys = new((int)math.pow(ChunkSize, 2), Allocator.Temp);

			FastNoise.GenUniformGrid2D(NoiseNodeTreePtr, continentalness, chunkPos.x, chunkPos.y, ChunkSize, ChunkSize, 2f, 1337);
			FastNoise.GenUniformGrid2D(NoiseNodeTreePtr, erosion, chunkPos.x, chunkPos.y, ChunkSize, ChunkSize, 2f, 1337);
			FastNoise.GenUniformGrid2D(NoiseNodeTreePtr, peaksandvalleys, chunkPos.x, chunkPos.y, ChunkSize, ChunkSize, 2f, 1337);

			//TODO: use NativeCurve to evaluate final noise

			return noiseOut;
		}

		private NativeArray<float> GenerateCaveNoise(int3 chunkPos)
		{
			NativeArray<float> noiseOut = new((int)math.pow(ChunkSize, 3), Allocator.Temp);

			FastNoise.GenUniformGrid3D(NoiseNodeTreePtr, noiseOut, chunkPos.x, chunkPos.y, chunkPos.z, ChunkSize, ChunkSize, ChunkSize, 2f, 1337);

			return noiseOut;
		}
	}
}