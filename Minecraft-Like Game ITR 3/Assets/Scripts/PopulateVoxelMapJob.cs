using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UtilityLibrary.Unity.Runtime;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct PopulateVoxelMapJob : IJob
{
	public struct VoxelMapData
	{
		public int                ChunkSize;
		public int                WorldSizeInVoxels;
		public BiomeAttributesJob BiomeData;
	}

	[ReadOnly]  public Vector3             Position;
	[ReadOnly]  public VoxelMapData        VoxelData;
	[WriteOnly] public NativeArray<ushort> VoxelMap;

	[WriteOnly] [NativeDisableUnsafePtrRestriction]
	public IntPtr NodeHandle;

	public void Execute()
	{
		PopulateVoxelMap();
	}

	private void PopulateVoxelMap()
	{
		for (var y = 0; y < VoxelData.ChunkSize; y++)
		for (var x = 0; x < VoxelData.ChunkSize; x++)
		for (var z = 0; z < VoxelData.ChunkSize; z++)
		{
			var posX = x + Position.x;
			var posY = y + Position.y;
			var posZ = z + Position.z;
			var voxel = WorldExtensions.GetVoxel(NodeHandle, posX, posY, posZ,
			                                     VoxelData.WorldSizeInVoxels,
			                                     VoxelData.BiomeData.BiomeScale,
			                                     VoxelData.BiomeData.BiomeHeight,
			                                     VoxelData.BiomeData.SolidGroundHeight);
			VoxelMap.SetAtFlatIndex(VoxelData.ChunkSize, x, y, z, voxel);
		}
	}
}