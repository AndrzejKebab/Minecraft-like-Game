using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(CompileSynchronously = true)]
public class WorldExtensions : MonoBehaviour
{
	[BurstCompile]
	public static ushort GetVoxel(float posX, float posY, float posZ, int WorldSizeInVoxels, int BiomeScale, int BiomeHeight, int SolidBiomeHeight)
	{
		BiomeAttributesJob biomeAttributes = new BiomeAttributesJob()
		{
			BiomeHeight = BiomeHeight,
			BiomeScale = BiomeScale,
			SolidGroundHeight = SolidBiomeHeight
		};

		int _yPos = Mathf.FloorToInt(posY);
		float _terrainHeight = NoiseGenerator.Get2DPerlin(posX, posZ, 0, 0, biomeAttributes.BiomeScale);
		_terrainHeight = Mathf.FloorToInt(_terrainHeight * biomeAttributes.BiomeHeight) + biomeAttributes.SolidGroundHeight;

		ushort _voxelValue = 2;

		if (!IsVoxelInWorld(posX, posY, posZ, WorldSizeInVoxels))
		{
			return 0; // if not in world return air
		}
		if (posY == 0)
		{
			return 1; // if bottom of chunk return bedrock
		}

		if (_yPos > _terrainHeight)
		{
			if (_yPos <= 256)
			{
				_voxelValue = 5; // if above ground and below 64 return sand(in future water)
			}
			else
			{
				_voxelValue = 0; // if above ground return air
			}
		}
		else if (_yPos == _terrainHeight)
		{
			_voxelValue = 4; // if on ground height return grassblock
		}
		else if (_yPos < _terrainHeight && _yPos > _terrainHeight - 6)
		{
			_voxelValue = 3; // if below ground and above ground - 6 return dirt
		}

		return _voxelValue;
	}

	[BurstCompile]
	public static bool IsVoxelInWorld(float posX, float posY, float posZ, int WorldSizeInVoxels)
	{
		if (posX >= -(WorldSizeInVoxels * 0.5f) && posX < (WorldSizeInVoxels * 0.5f) &&
			posY >= -(WorldSizeInVoxels * 0.5f) && posY < (WorldSizeInVoxels * 0.5f) &&
			posZ >= -(WorldSizeInVoxels * 0.5f) && posZ < (WorldSizeInVoxels * 0.5f))
		{
			return true;
		}
		else
		{
			return false;
		}
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