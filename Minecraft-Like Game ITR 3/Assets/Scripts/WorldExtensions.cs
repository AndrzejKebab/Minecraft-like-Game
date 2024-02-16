using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class WorldExtensions : MonoBehaviour
{
	[BurstCompile]
	public static ushort GetVoxel(float posX, float posY, float posZ, int worldSizeInVoxels, int biomeScale, int biomeHeight, int solidBiomeHeight)
	{
		var biomeAttributes = new BiomeAttributesJob()
		{
			BiomeHeight = biomeHeight,
			BiomeScale = biomeScale,
			SolidGroundHeight = solidBiomeHeight
		};

		var yPos = Mathf.FloorToInt(posY);
		var terrainHeight = NoiseGenerator.Get2DPerlin(posX, posZ, 0, 0, biomeAttributes.BiomeScale);
		terrainHeight = Mathf.FloorToInt(terrainHeight * biomeAttributes.BiomeHeight) + biomeAttributes.SolidGroundHeight;

		ushort voxelValue = 2;

		if (!IsVoxelInWorld(posX, posY, posZ, worldSizeInVoxels))
		{
			return 0;
		}
		if (posY == 0)
		{
			return 1;
		}

		if (yPos > terrainHeight)
		{
			voxelValue = yPos <= 256 ? (ushort)5 : (ushort)0;
		}
		else if (yPos == terrainHeight)
		{
			voxelValue = 4;
		}
		else if (yPos < terrainHeight && yPos > terrainHeight - 6)
		{
			voxelValue = 3;
		}

		return voxelValue;
	}

	[BurstCompile]
	public static bool IsVoxelInWorld(float posX, float posY, float posZ, int worldSizeInVoxels)
	{
		return posX >= -(worldSizeInVoxels * 0.5f) && posX < (worldSizeInVoxels * 0.5f) &&
		       posY >= -(worldSizeInVoxels * 0.5f) && posY < (worldSizeInVoxels * 0.5f) &&
		       posZ >= -(worldSizeInVoxels * 0.5f) && posZ < (worldSizeInVoxels * 0.5f);
	}

	[BurstCompile]
	public static int FlattenIndex(int posX, int posY, int posZ, int chunkSize) => math.abs((posZ * chunkSize * chunkSize) + (posY * chunkSize) + posX);

	//public static bool CheckForVoxel(Vector3 pos)
	//{
	//	int3 thisChunk = new int3(math.floor(pos) / VoxelData.ChunkSize);
	//
	//	if (!World.instance.IsChunkInWorld(thisChunk))
	//	{
	//		return false;
	//	}
	//
	//	if (World.instance.ChunkStorage[thisChunk] != null)
	//	{
	//		return World.instance.blockTypesJobs[World.instance.ChunkStorage[thisChunk].GetVoxelFromGlobalVector3(pos)].IsSolid;
	//	}
	//
	//	return World.instance.blockTypesJobs[GetVoxel(pos.x, pos.y, pos.z, VoxelData.ChunkSize, World.instance.biomeAttributesJob.BiomeScale, World.instance.biomeAttributesJob.BiomeHeight, World.instance.biomeAttributesJob.SolidGroundHeight)].IsSolid;
	//}
}