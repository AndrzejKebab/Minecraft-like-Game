using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
	[field:SerializeField] public Transform playerTransform { get; private set; }
	[SerializeField] private Vector3 playerSpawnPosition;
	[SerializeField] private int seed;
	[SerializeField] private BiomeAttributes biomeAttributes;

	public BiomeAttributeData BiomeAttributeData { get; private set; }

	[field:SerializeField] public Material Material { get; private set; }
	public BlockType[] blockTypes;

	public NativeArray<BlockType> BlockTypes { get; private set; }

	private Dictionary<int2, Chunk> ChunkStorage = new Dictionary<int2, Chunk>();

	private List<int2> activeChunks = new List<int2>();
	private List<int2> chunksToCreate = new List<int2>();
	public int2 playerChunkCoord { get; private set; }
	private int2 playerLastChunkCoord;
	private byte lastViewDistance = VoxelData.ViewDistanceInChunks;

	private void Awake()
	{
		BlockTypes = new NativeArray<BlockType>(blockTypes, Allocator.Persistent);
		BiomeAttributeData = new BiomeAttributeData
		{
			BiomeScale = biomeAttributes.BiomeScale,
			BiomeHeight = biomeAttributes.BiomeHeight,
			SolidBiomeHeight = biomeAttributes.SolidGroundHeight
		};
	}

	private void Start()
	{
		Random.InitState(seed);
		playerSpawnPosition = new Vector3(0, VoxelData.ChunkHeight, 0);
		
		GenerateWorld();

		playerLastChunkCoord = GetChunkCoordFromVector3(playerTransform.position);
	}

	private void Update()
	{
		playerChunkCoord = GetChunkCoordFromVector3(playerTransform.position);
		if (!playerChunkCoord.Equals(playerLastChunkCoord) || VoxelData.ViewDistanceInChunks != lastViewDistance)
		{
			CheckViewDistance();
			lastViewDistance = VoxelData.ViewDistanceInChunks;
		}

		if(chunksToCreate.Count > 0)
		{
			for(int i = 0; i < chunksToCreate.Count; i++)
			{
				if (!ChunkStorage[chunksToCreate[i]].IsScheduled)
				{
					ChunkStorage[chunksToCreate[i]].Initialise();
				}

				if (ChunkStorage[chunksToCreate[i]].IsScheduled && ChunkStorage[chunksToCreate[i]].IsCompleted)
				{
					ChunkStorage[chunksToCreate[i]].CreateMesh();
					chunksToCreate.RemoveAt(i);
				}
			}
		}
	}

	private void GenerateWorld()
	{
		for (int x = -VoxelData.ViewDistanceInChunks + 1; x < VoxelData.ViewDistanceInChunks; x++)
		{
			for (int z =  -VoxelData.ViewDistanceInChunks + 1; z < VoxelData.ViewDistanceInChunks; z++)
			{
				CreateNewChunk(x, z);
				activeChunks.Add(new int2(x, z));
			}
		}

		playerTransform.position = playerSpawnPosition;
	}

	public int2 GetChunkCoordFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

		return new int2(x, z);
	}

	private void CheckViewDistance()
	{
		int2 _coord = GetChunkCoordFromVector3(playerTransform.position);
		List<int2> _previuslyActiveChunks = new List<int2>(activeChunks);
		activeChunks.Clear();

		playerLastChunkCoord = playerChunkCoord;

		for (int x = _coord.x - VoxelData.ViewDistanceInChunks; x < _coord.x + VoxelData.ViewDistanceInChunks; x++)
		{
			for (int z = _coord.y - VoxelData.ViewDistanceInChunks; z < _coord.y + VoxelData.ViewDistanceInChunks; z++)
			{
				if (IsChunkInWorld(new int2(x, z)))
				{
					if (!ChunkStorage.ContainsKey(new int2(x, z)))
					{
						CreateNewChunk(x,z);
					}
					else if (!ChunkStorage[new int2(x, z)].IsActive)
					{
						ChunkStorage[new int2(x, z)].IsActive = true;
					}
					activeChunks.Add(new int2(x, z));
					_previuslyActiveChunks.Remove(new int2(x, z));
				}
			}
		}

		foreach (var c in _previuslyActiveChunks)
		{
			ChunkStorage[new int2(c.x, c.y)].IsActive = false;
		}
		_previuslyActiveChunks.Clear();
	}

	private void CreateNewChunk(int x, int z)
	{
		ChunkStorage[new int2(x, z)] = new Chunk(new int2(x, z), this);
		chunksToCreate.Add(new int2(x, z));
	}

	private bool IsChunkInWorld(int2 coord)
	{
		if (coord.x >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.x < (VoxelData.WorldSizeInChunks * 0.5f) &&
		    coord.y >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.y < (VoxelData.WorldSizeInChunks * 0.5f))
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}

[BurstCompile]
public static class WorldExtensions
{
	[BurstCompile]
	public static short GetVoxel(float posX, float posY, float posZ, int ChunkHeight, int WorldSizeInVoxels, int BiomeScale, int BiomeHeight, int SolidBiomeHeight)
	{
		BiomeAttributeData biomeAttributes = new BiomeAttributeData(BiomeScale, BiomeHeight, SolidBiomeHeight);
		float3 pos = new float3(posX, posY, posZ);
		int _yPos = Mathf.FloorToInt(pos.y);
		float _terrainHeight = NoiseGenerator.Get2DPerlin(pos.x, pos.z, 0, 0, biomeAttributes.BiomeScale);
		_terrainHeight = Mathf.FloorToInt(_terrainHeight * biomeAttributes.BiomeHeight) + biomeAttributes.SolidBiomeHeight;

		short _voxelValue = 2;

		if (!IsVoxelInWorld(pos, ChunkHeight, WorldSizeInVoxels))
		{
			return 0; // if not in world return air
		}
		if (pos.y == 0)
		{
			return 1; // if bottom of chunk return bedrock
		}

		if (_yPos > _terrainHeight)
		{
			if (_yPos <= 64)
			{
				_voxelValue = 5; // if above ground and below 64 return cobblestone(in future water)
			}
			else
			{
				_voxelValue = 0; // if above ground return air
			}
		}
		else if(_yPos == _terrainHeight)
		{
			_voxelValue = 3; // if on ground height return grassblock
		}
		else if(_yPos < _terrainHeight && _yPos > _terrainHeight - 6)
		{
			_voxelValue = 4; // if below ground and above ground - 6 return dirt
		}

		return _voxelValue;
	}

	public static bool IsVoxelInWorld(float3 pos, int ChunkHeight, int WorldSizeInVoxels)
	{
		if (pos.x >= -(WorldSizeInVoxels * 0.5f) && pos.x < (WorldSizeInVoxels * 0.5f) &&
		    pos.y >= 0 && pos.y < ChunkHeight &&
		    pos.z >= -(WorldSizeInVoxels * 0.5f) && pos.z < (WorldSizeInVoxels * 0.5f))
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	[BurstCompile]
	public static int FattenIndex(int posX, int posY, int posZ, int ChunkWidth, int ChunkHeight) => (posZ * ChunkWidth * ChunkHeight) +
																									(posY * ChunkWidth) + posX;
}

public struct BiomeAttributeData
{
	public int BiomeScale;
	public int BiomeHeight;
	public int SolidBiomeHeight;

	public BiomeAttributeData(int biomeScale, int biomeHeight, int solidBiomeHeight)
	{
		BiomeScale = biomeScale;
		BiomeHeight = biomeHeight;
		SolidBiomeHeight = solidBiomeHeight;
	}
}

[System.Serializable]
public struct BlockType
{
	[Header("Block Data")]
	public ushort BlockID;
	public bool IsSolid;

	[Header("Texture Atlas Index")]
	public short BackFaceTexture;
	public short FrontFaceTexture;
	public short TopFaceTexture;
	public short BottomFaceTexture;
	public short LeftFaceTexture;
	public short RightFaceTexture;

	public int GetTexture2D(int faceIndex)
	{
		switch (faceIndex)
		{
			case 0:
				return BackFaceTexture;
			case 1:
				return FrontFaceTexture;
			case 2:
				return TopFaceTexture;
			case 3: 
				return BottomFaceTexture;
			case 4:
				return LeftFaceTexture;
			case 5:
				return RightFaceTexture;
			default:
				Debug.LogError("GetTexture2D: Wrong Face Index!");
				return 0;
		}
	}
}