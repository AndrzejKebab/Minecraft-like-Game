using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class WorldExtensions : MonoBehaviour
{
	[BurstCompile]
	public static ushort GetVoxel(IntPtr nodeHandle, float posX,        float posY, float posZ, int worldSizeInVoxels,
	                              int    biomeScale, int   biomeHeight, int   solidBiomeHeight)
	{
		var biomeAttributes = new BiomeAttributesJob
		                      {
			                      BiomeHeight       = biomeHeight,
			                      BiomeScale        = biomeScale,
			                      SolidGroundHeight = solidBiomeHeight
		                      };

		var yPos          = Mathf.FloorToInt(posY);
		var terrainHeight = NoiseGenerator.Get2DPerlin(nodeHandle, posX, posZ, biomeAttributes.BiomeScale);
		terrainHeight = Mathf.FloorToInt(terrainHeight * biomeAttributes.BiomeHeight) +
		                biomeAttributes.SolidGroundHeight;

		ushort voxelValue = 2;

		if (!IsVoxelInWorld(posX, posY, posZ, worldSizeInVoxels)) return 0;
		if (posY == 0) return 1;

		if (yPos > terrainHeight)
			voxelValue = yPos <= 256 ? (ushort)5 : (ushort)0;
		else if (yPos == terrainHeight)
			voxelValue                                                        = 4;
		else if (yPos < terrainHeight && yPos > terrainHeight - 6) voxelValue = 3;

		return voxelValue;
	}

	[BurstCompile]
	private static bool IsVoxelInWorld(float posX, float posY, float posZ, int worldSizeInVoxels)
	{
		return posX >= -(worldSizeInVoxels * 0.5f) && posX < worldSizeInVoxels * 0.5f &&
		       posY >= -(worldSizeInVoxels * 0.5f) && posY < worldSizeInVoxels * 0.5f &&
		       posZ >= -(worldSizeInVoxels * 0.5f) && posZ < worldSizeInVoxels * 0.5f;
	}

	[BurstCompile]
	public static int FlattenIndex(int posX, int posY, int posZ)
	{
		return posX | (posY << 5) | (posZ << 10);
	}

	[BurstCompile]
	public static void UnflattenIndex(in int index, out int3 pos)
	{
		var x = index & 0x1f;
		var y = (index >> 5) & 0x1f;
		var z = (index >> 10) & 0x1f;

		pos = new int3(x, y, z);
	}
}