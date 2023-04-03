using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile(CompileSynchronously = true)]
public struct PopulateVoxelMapJob : IJob
{
	public struct VoxelMapData
	{
		public int ChunkWidth;
		public int ChunkHeight;
		public int WorldSizeInVoxels;
		public BiomeAttributeData BiomeAttributeData;
	}

	[ReadOnly]
	public Vector3 Position;
	[ReadOnly]
	public VoxelMapData VoxelData;
	[WriteOnly]
	public NativeArray<short> VoxelMap;

	public void Execute()
	{
		for (int y = 0; y < VoxelData.ChunkHeight; y++)
		{
			for (int x = 0; x < VoxelData.ChunkWidth; x++)
			{
				for (int z = 0; z < VoxelData.ChunkWidth; z++)
				{
					VoxelMap[x + VoxelData.ChunkWidth * (y + VoxelData.ChunkHeight * z)] = WorldExtensions.GetVoxel(new Vector3(x, y, z) + Position, VoxelData.ChunkHeight, VoxelData.WorldSizeInVoxels, VoxelData.BiomeAttributeData);
				}
			}
		}
	}
}