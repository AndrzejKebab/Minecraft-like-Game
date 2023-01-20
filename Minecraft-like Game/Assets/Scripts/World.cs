using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class World : MonoBehaviour
{
	[SerializeField] private Transform playerTransform;
	[SerializeField] private Vector3 playerSpawnPosition;
	[field:SerializeField] public Material Material { get; private set; }
	[SerializeField] public BlockType[] blockTypes;

	private Dictionary<ushort, string> blocks = new Dictionary<ushort, string>();
	[SerializeField] private string[] blocksName;

	public NativeArray<BlockType> BlockTypes { get; private set; }

	private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

	private List<ChunkCoord> activeChunks = new List<ChunkCoord>();
	private ChunkCoord playerChunkCoord;
	private ChunkCoord playerLastChunkCoord;

	private void Awake()
	{
		BlockTypes = new NativeArray<BlockType>(blockTypes, Allocator.Persistent);

		short i = 0;
		foreach (var _blockType in blockTypes)
		{
			blocks.Add(_blockType.BlockID, blocksName[i]);
			i++;
		}

		string _text = "";
		foreach (var block in blocks)
		{
			_text += block.Key + ": " + block.Value + "\n";
		}
		Debug.Log(_text);
	}

	private void Start()
	{
		playerSpawnPosition = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2,
										VoxelData.ChunkHeight,
										(VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2);
		
		GenerateWorld();

		playerLastChunkCoord = GetChunkCoordFromVector3(playerTransform.position);
	}

	private void Update()
	{
		playerChunkCoord = GetChunkCoordFromVector3(playerTransform.position);
		if (!playerChunkCoord.Equals(playerLastChunkCoord))
		{
			CheckViewDistance();
		}
			
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

		playerTransform.position = playerSpawnPosition;
	}

	private ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

		return new ChunkCoord(x, z);
	}

	private void CheckViewDistance()
	{
		ChunkCoord _coord = GetChunkCoordFromVector3(playerTransform.position);
		List<ChunkCoord> _previuslyActiveChunks = new List<ChunkCoord>(activeChunks);

		playerLastChunkCoord = playerChunkCoord;

		for (int x = _coord.X - VoxelData.ViewDistanceInChunks; x < _coord.X + VoxelData.ViewDistanceInChunks; x++)
		{
			for (int z = _coord.Z - VoxelData.ViewDistanceInChunks; z < _coord.Z + VoxelData.ViewDistanceInChunks; z++)
			{
				if (IsChunkInWorld(new ChunkCoord(x, z)))
				{
					if (chunks[x, z] == null)
					{
						CreateNewChunk(x,z);
					}
					else if (!chunks[x, z].IsActive)
					{
						chunks[x, z].IsActive = true;
						activeChunks.Add(new ChunkCoord(x, z));
					}
				}

				for (int i = 0; i < _previuslyActiveChunks.Count; i++)
				{
					if (_previuslyActiveChunks[i].Equals(new ChunkCoord(x, z)))
					{
						_previuslyActiveChunks.RemoveAt(i);
					}
				}
			}
		}

		foreach (var c in _previuslyActiveChunks)
		{
			chunks[c.X, c.Z].IsActive = false;
		}
	}

	public ushort GetVoxel(Vector3 pos)
	{
		if (!IsVoxelInWorld(pos))
			return 0;
		if (pos.y < 1)
		{
			return  blockTypes[1].BlockID;
		}
		else if (pos.y == VoxelData.ChunkHeight - 1)
		{
			return blockTypes[3].BlockID;
		}
		else
		{
			return blockTypes[2].BlockID;
		}
	}

	private void CreateNewChunk(int x, int z)
	{
		chunks[x, z] = new Chunk(new ChunkCoord(x, z), this);
		activeChunks.Add(new ChunkCoord(x, z));
	}

	private bool IsChunkInWorld(ChunkCoord coord)
	{
		if (coord.X > 0 && coord.X < VoxelData.WorldSizeInChunks &&
		    coord.Z > 0 && coord.Z < VoxelData.WorldSizeInChunks)
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	private bool IsVoxelInWorld(Vector3 pos)
	{
		if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels &&
		    pos.y >= 0 && pos.y < VoxelData.ChunkHeight &&
		    pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
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