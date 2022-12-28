using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
	[Header("World Generation Data")]
	[SerializeField] private int seed;
	[SerializeField] private BiomeAttributes biomeAttributes;

	[Header("Player Data")]
	[SerializeField] private Transform player;
	[SerializeField] private Vector3 spawnPosition;

	[field:Header("Blocks Data")]
	[field:SerializeField] public Material Material { get; private set; }
	[field:SerializeField] public BlockType[] BlockTypes { get; private set; }

	private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

	private List<ChunkCoord> activeChunks = new List<ChunkCoord>();

	private ChunkCoord playerChunkCoord;
	private ChunkCoord playerLastChunkCoord;

	private void Start()
	{
		Random.InitState(seed);

		spawnPosition = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2, VoxelData.ChunkHeight - 40, (VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2);

		GenerateWorld();	
		
		playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
	}

	private void Update()
	{
		//playerChunkCoord = GetChunkCoordFromVector3(player.position);
		//
		//if (!playerChunkCoord.Equals(playerLastChunkCoord))
		//{
		//	CheckViewDistance();
		//	playerLastChunkCoord = playerChunkCoord;
		//}
	}

	private void GenerateWorld()
	{
		for (int x = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; x < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; x++)
		{
			for (int z = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; z < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; z++)
			{
				CreateNewChunk(x, z);
			}
		}
		player.position = spawnPosition;
	}

	private ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
		return new ChunkCoord(x, z);
	}

	private void CheckViewDistance()
	{
		ChunkCoord _coord = GetChunkCoordFromVector3(player.position);

		List<ChunkCoord> _previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

		for (int x = _coord.x - VoxelData.ViewDistanceInChunks; x < _coord.x + VoxelData.ViewDistanceInChunks; x++)
		{
			for (int z = _coord.z - VoxelData.ViewDistanceInChunks; z < _coord.z + VoxelData.ViewDistanceInChunks; z++)
			{
				if (IsChunkInWorld(new ChunkCoord(x, z)))
				{
					if (chunks[x, z] == null)
					{
						CreateNewChunk(x, z);
					}
					else if (!chunks[x, z].IsActive)
					{
						chunks[x, z].IsActive = true;
						activeChunks.Add(new ChunkCoord(x, z));
					}
				}

				for (int i = 0; i < _previouslyActiveChunks.Count; i++)
				{
					if (_previouslyActiveChunks[i].Equals(new ChunkCoord(x, z)))
					{
						_previouslyActiveChunks.RemoveAt(i);
					}
				}
			}
		}

		foreach(ChunkCoord chunk in _previouslyActiveChunks)
		{
			chunks[chunk.x, chunk.z].IsActive = false;
		}
	}

	public bool CheckForVoxel(float x, float y, float z)
	{
		int xCheck = Mathf.FloorToInt(x);
		int yCheck = Mathf.FloorToInt(y);
		int zCheck = Mathf.FloorToInt(z);

		int xChunk = xCheck / VoxelData.ChunkWidth;
		int zChunk = zCheck / VoxelData.ChunkWidth;

		xCheck -= xChunk * VoxelData.ChunkWidth;
		zCheck -= zChunk * VoxelData.ChunkWidth;

		return BlockTypes[chunks[xChunk, zChunk].voxelMap[xCheck, yCheck, zCheck]].isSolid;
	}

	public byte GetVoxel(Vector3 position)
	{

		int _yPosition = Mathf.FloorToInt(position.y);

		if (!IsVoxelInWorld(position))
		{
			return 0;
		}

		if (_yPosition == 0)
		{
			return 1;
		}

		int _terrainHeight = Mathf.FloorToInt(biomeAttributes.TerrainHeight * HeightMap.Get2DPerlin(new Vector2(position.x, position.z), 0, biomeAttributes.TerrainScale)) + biomeAttributes.SolidGroundHeight;
		byte _voxelValue = 0;

		if (_yPosition == _terrainHeight)
		{
			_voxelValue = 2;
		}
		else if (_yPosition < _terrainHeight && _yPosition > _terrainHeight - 6)
		{
			_voxelValue = 3;
		}
		else if (_yPosition > _terrainHeight)
		{
			return 0;
		}
		else
		{
			_voxelValue = 4;
		}

		if (_voxelValue == 4)
		{
			foreach(Lode lode in biomeAttributes.Lodes)
			{
				if(_yPosition > lode.MinHeight && _yPosition < lode.MaxHeight)
				{
					if(HeightMap.Get3DPerlin(position, lode.NoiseOffset, lode.Scale, lode.MinThreshold, lode.MaxThreshold))
					{
						_voxelValue = lode.BlockID;
					}
				}
			}
		}		
		return _voxelValue;		
	}

	private void CreateNewChunk(int x, int z)
	{
		if (!IsChunkInWorld(new ChunkCoord(x, z))) return;
		chunks[x, z] = new Chunk(new ChunkCoord(x, z), this);
		activeChunks.Add(new ChunkCoord(x, z));
	}

	private bool IsChunkInWorld (ChunkCoord coord)
	{
		if (coord.x >= 0 && coord.x < VoxelData.WorldSizeInChunks && coord.z >= 0 && coord.z < VoxelData.WorldSizeInChunks)
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	private bool IsVoxelInWorld(Vector3 position)
	{
		if (position.x >= 0 && position.x < VoxelData.WorldSizeInVoxels && position.y >= 0 && position.y < VoxelData.ChunkHeight && position.z >= 0 && position.z < VoxelData.WorldSizeInVoxels)
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}

[System.Serializable]
public class BlockType
{
	[field:SerializeField] public string BlockName { get; private set; }
	[field:SerializeField] public bool isSolid { get; private set;}

	[Header("Texture Values")]
	[SerializeField] private int backFaceTextture;
	[SerializeField] private int frontFaceTextture;
	[SerializeField] private int topFaceTextture;
	[SerializeField] private int bottomFaceTextture;
	[SerializeField] private int leftFaceTextture;
	[SerializeField] private int rightFaceTextture;

	public int GetTextureID (int faceIndexID)
	{
		switch (faceIndexID)
		{
			case 0:
				return backFaceTextture;
			case 1:
				return frontFaceTextture;
			case 2:
				return topFaceTextture;
			case 3:
				return bottomFaceTextture;
			case 4:
				return leftFaceTextture;
			case 5:
				return rightFaceTextture;
			default:
				Debug.Log("Ding Dong something went wrong!");
				return 0;
		}
	}
}