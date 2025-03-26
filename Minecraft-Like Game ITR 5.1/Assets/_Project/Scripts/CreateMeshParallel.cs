using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UtilityLibrary.Unity.Runtime;

namespace PatataGames;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct CreateMeshParallel : IJobFor
{
	public NativeParallelHashMap<int3, Chunk>.ReadOnly ChunkMap;
	public NativeList<int3> ChunksToUpdate;
	public Mesh.MeshDataArray[] MeshDataArray;
	
	private int3 chunkPos;
	
	public void Execute(int index)
	{
		chunkPos = ChunksToUpdate[index];
		CreateMesh(chunkPos);
	}
	
	private void CreateMesh(int3 chunkPos)
	{
		for (int x = 0; x < ChunkSystem.ChunkSize; x++)
		{
			for (int z = 0; z < ChunkSystem.ChunkSize; z++)
			{
				for (int y = 0; y < ChunkSystem.ChunkSize; y++)
				{
					if (ChunkMap[chunkPos].VoxelMap.GetAtFlatIndex(ChunkSystem.ChunkSize, x, y, z) == 1)
					{
						AddVoxelData(new int3(x,y,z));
					}
				}
			}
		}
	}

	private void AddVoxelData(int3 voxelPos)
	{
		for (int p = 0; p < 6; p++)
		{
			if (IsFaceVisible(new int3(voxelPos.x, voxelPos.y, voxelPos.z)))
			{
				
			}
		}
	}

	private bool IsFaceVisible(int3 voxelPos)
	{
		return ChunkMap[chunkPos].VoxelMap.GetAtFlatIndex(ChunkSystem.ChunkSize, voxelPos.x, voxelPos.y, voxelPos.z) == 1; 
	}
}