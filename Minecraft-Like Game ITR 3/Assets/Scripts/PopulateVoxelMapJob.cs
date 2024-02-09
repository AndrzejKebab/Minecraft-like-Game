using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UtilityLibrary.Unity.Runtime;

[BurstCompile(CompileSynchronously = true)]
public struct PopulateVoxelMapJob : IJob
{
	public struct VoxelMapData
	{
		public int ChunkSize;
		public int WorldSizeInVoxels;
		public BiomeAttributesJob BiomeData;
	}

	[ReadOnly]
	public Vector3 Position;
	[ReadOnly]
	public VoxelMapData VoxelData;
	[WriteOnly]
	public NativeArray<ushort> VoxelMap;

	public void Execute()
	{
		PopulateVoxelMap();
	}

	private void PopulateVoxelMap()
	{
		for (int y = 0; y < VoxelData.ChunkSize; y++)
		{
			for (int x = 0; x < VoxelData.ChunkSize; x++)
			{
				for (int z = 0; z < VoxelData.ChunkSize; z++)
				{
					float posX = x + Position.x;
					float posY = y + Position.y;
					float posZ = z + Position.z;
					var voxel = WorldExtensions.GetVoxel(posX, posY, posZ,
						VoxelData.WorldSizeInVoxels,
						VoxelData.BiomeData.BiomeScale,
						VoxelData.BiomeData.BiomeHeight,
						VoxelData.BiomeData.SolidGroundHeight);
					VoxelMap.SetAtFlatIndex(VoxelData.ChunkSize, x, y, z, voxel);
				}
			}
		}
	}
}