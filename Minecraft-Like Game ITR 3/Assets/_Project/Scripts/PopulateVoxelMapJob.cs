using NativeTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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

	[ReadOnly] public NativeTexture2D<float>.ReadOnly HeightMap;

	[ReadOnly]  public VoxelMapData        VoxelData;
	[ReadOnly]  public int3                ChunkPosition;
	[WriteOnly] public NativeArray<ushort> VoxelMap;

	public void Execute()
	{
		for (var y = 0; y < VoxelData.ChunkSize; y++)
		for (var x = 0; x < VoxelData.ChunkSize; x++)
		for (var z = 0; z < VoxelData.ChunkSize; z++)
		{
			float posX = ChunkPosition.x + x;
			float posY = ChunkPosition.y + y;
			float posZ = ChunkPosition.z + z;

			if (!IsVoxelInWorld(posX, posY, posZ, VoxelData.WorldSizeInVoxels))
			{
				VoxelMap.SetAtFlatIndex(VoxelData.ChunkSize, x, y, z, (ushort)0);
				continue;
			}

			var rawNoise = HeightMap[new int2(x, z)];
			var terrainHeight = NoiseGenerator.HeightFromNoise(
			                                                   rawNoise,
			                                                   VoxelData.BiomeData.BiomeHeight,
			                                                   VoxelData.BiomeData.SolidGroundHeight);

			VoxelMap.SetAtFlatIndex(VoxelData.ChunkSize, x, y, z,
			                        NoiseGenerator.ClassifyVoxel((int)posY, terrainHeight,
			                                                     VoxelData.BiomeData.SolidGroundHeight));
		}
	}

	[BurstCompile]
	private static bool IsVoxelInWorld(float posX, float posY, float posZ, int worldSizeInVoxels)
	{
		var half = worldSizeInVoxels * 0.5f;
		return posX >= -half && posX < half &&
		       posY >= -half && posY < half &&
		       posZ >= -half && posZ < half;
	}
}