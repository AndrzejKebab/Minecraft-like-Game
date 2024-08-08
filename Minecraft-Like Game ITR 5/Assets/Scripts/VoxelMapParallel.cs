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

			var noiseValues = GenerateNoise(chunkPos);

			var blockIdList = new NativeList<int>(Allocator.Temp);

			var currentBlockId = GetBlock(noiseValues[0], chunkPos.y);
			var count = 0;

			for (int i = 0; i < noiseValues.Length; i++)
			{
				int posY = (i / 32) % 32 + chunkPos.y;
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

		private NativeArray<float> GenerateNoise(int3 chunkPos)
		{
			NativeArray<float> noiseOut = new((int)math.pow(32, 3), Allocator.Temp);

			FastNoise.GenUniformGrid3D(NoiseNodeTreePtr, noiseOut, chunkPos.x, chunkPos.y, chunkPos.z, 32, 32, 32, 2f, 1337);
			
			return noiseOut;
		}

		private int GetBlock(float noiseValue, int yPos)
		{
			// calculate terrain height and subtract MaxTerrainHeight / 2 so noiseValue of 0.5 will be at Y = 0 (a sea level)
			int terrainHeight = (int)math.floor((noiseValue * MaxTerrainHeight) - (MaxTerrainHeight * 0.5f));
			
			int voxelID = 1; // default block is Stone

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

			return voxelID;
		}
	}
}