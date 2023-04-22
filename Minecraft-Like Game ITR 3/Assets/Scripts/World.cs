using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
	public static World instance;

	[SerializeField] private BlockTypes[] blockTypes;
	public BlockTypes[] BlockTypes { get { return blockTypes; } }
	public NativeArray<BlockTypesJob> blockTypesJobs;
	[field:SerializeField] public Material Material { get; private set; }
	[SerializeField] private int seed;
	[SerializeField] private BiomeAttributes biomeAttributes;
	public BiomeAttributesJob biomeAttributesJob { get; private set; }

	private Dictionary<int3, Chunk> chunkStorage = new Dictionary<int3, Chunk>();
	public Dictionary<int3, Chunk> ChunkStorage { get { return chunkStorage; } }
	private List<int3> chunksToCreate = new List<int3>();
	private List<int3> activeChunks = new List<int3>();

	public Transform PlayerTransfrom { get; private set; }
	public int3 playerChunkCoord { get; private set; }
	private int3 playerLastChunkCoord;
	private byte lastViewDistance = VoxelData.ViewDistanceInChunks;

	public Bounds ChunkBound { get; private set; }

	private void Awake()
	{
		instance = this;

		Vector3 _size = new Vector3(VoxelData.ChunkSize, VoxelData.ChunkSize, VoxelData.ChunkSize);
		ChunkBound = new Bounds(_size / 2, _size);

		blockTypesJobs = new NativeArray<BlockTypesJob>(blockTypes.Length, Allocator.Persistent);
		for(int i = 0; i < blockTypes.Length; i++)
		{
			blockTypesJobs[i] = blockTypes[i].BlockTypeData;
		};

		biomeAttributesJob = biomeAttributes.BiomeData;

		PlayerTransfrom = GameObject.Find("PlayerCapsule").GetComponent<Transform>();
	}

	void Start()
	{
		Random.InitState(seed);
		//GenerateWorld();
		CheckViewDistance();
	}

	//private void GenerateWorld()
	//{
	//	for(int y = -VoxelData.ViewDistanceInChunks / 4; y < VoxelData.ViewDistanceInChunks; y++)
	//	{
	//		for(int x = -VoxelData.ViewDistanceInChunks; x < VoxelData.ViewDistanceInChunks; x++)
	//		{
	//			for(int z = -VoxelData.ViewDistanceInChunks; z < VoxelData.ViewDistanceInChunks; z++)
	//			{
	//				CreateNewChunk(x, y, z);
	//				activeChunks.Add(new int3(x, y, z));
	//			}
	//		}
	//	}
	//
	//	playerLastChunkCoord = GetChunkCoordFromVector3(PlayerTransfrom.position);
	//}

	void Update()
	{
		playerChunkCoord = GetChunkCoordFromVector3(PlayerTransfrom.position);
		if (!playerChunkCoord.Equals(playerLastChunkCoord) || VoxelData.ViewDistanceInChunks != lastViewDistance)
		{
			CheckViewDistance();
			lastViewDistance = VoxelData.ViewDistanceInChunks;
		}

		if (chunksToCreate.Count > 0)
		{
			for (int i = 0; i < chunksToCreate.Count; i++)
			{
				if (!chunkStorage[chunksToCreate[i]].IsScheduled)
				{
					chunkStorage[chunksToCreate[i]].Initialise();
				}

				if (chunkStorage[chunksToCreate[i]].IsScheduled && chunkStorage[chunksToCreate[i]].IsCompleted)
				{
					chunkStorage[chunksToCreate[i]].CreateMesh();
					chunksToCreate.RemoveAt(i);
				}
			}
		}
	}

	private void CheckViewDistance()
	{
		int3 _coord = GetChunkCoordFromVector3(PlayerTransfrom.position);
		List<int3> _previuslyActiveChunks = new List<int3>();
		_previuslyActiveChunks.AddRange(activeChunks);
		activeChunks.Clear();
	
		playerLastChunkCoord = playerChunkCoord;
	
		for (int y = _coord.y - VoxelData.ViewDistanceInChunks / 4; y < _coord.y + VoxelData.ViewDistanceInChunks; y++)
		{
			for (int x = _coord.x - VoxelData.ViewDistanceInChunks; x < _coord.x + VoxelData.ViewDistanceInChunks; x++)
			{
				for(int z = _coord.z - VoxelData.ViewDistanceInChunks; z < _coord.z + VoxelData.ViewDistanceInChunks; z++)
				{
					if (IsChunkInWorld(new int3(x, y, z)))
					{
						if (!chunkStorage.ContainsKey(new int3(x, y, z)))
						{
							CreateNewChunk(x, y, z);
						}
						else if (!chunkStorage[new int3(x, y, z)].IsActive)
						{
							chunkStorage[new int3(x, y, z)].IsActive = true;
						}
						activeChunks.Add(new int3(x, y, z));
						_previuslyActiveChunks.Remove(new int3(x, y, z));
					}
				}
			}
		}
	
		foreach (var chunk in _previuslyActiveChunks)
		{
			chunkStorage[new int3(chunk.x, chunk.y, chunk.z)].IsActive = false;
		}
		_previuslyActiveChunks.Clear();
	}

	private void CreateNewChunk(int x, int y, int z)
	{
		chunkStorage[new int3(x, y, z)] = new Chunk(new int3(x, y, z), this);
		chunksToCreate.Add(new int3(x, y, z));
	}

	public int3 GetChunkCoordFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkSize);
		int y = Mathf.FloorToInt(pos.y / VoxelData.ChunkSize);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkSize);

		return new int3(x, y, z);
	}

	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkSize);
		int y = Mathf.FloorToInt(pos.y / VoxelData.ChunkSize);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkSize);

		return chunkStorage[new int3(x, y, z)];
	}

	public bool IsChunkInWorld(int3 coord)
	{
		if (coord.x >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.x < (VoxelData.WorldSizeInChunks * 0.5f) &&
			coord.y >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.y < (VoxelData.WorldSizeInChunks * 0.5f) &&
			coord.z >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.z < (VoxelData.WorldSizeInChunks * 0.5f))
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	public bool CheckForVoxel(Vector3 pos)
	{
		int3 thisChunk = new int3(math.floor(pos) / VoxelData.ChunkSize);

		if (!IsChunkInWorld(thisChunk))
		{
			return false;
		}

		if (ChunkStorage.ContainsKey(thisChunk) && !chunkStorage[thisChunk].IsUpdating)
		{
			return blockTypes[chunkStorage[thisChunk].GetVoxelFromGlobalVector3(pos)].BlockTypeData.IsSolid;
		}

		return blockTypes[WorldExtensions.GetVoxel(pos.x ,pos.y, pos.z, VoxelData.ChunkSize, biomeAttributesJob.BiomeScale, biomeAttributesJob.BiomeHeight, biomeAttributesJob.SolidGroundHeight)].BlockTypeData.IsSolid;
	}

	private void OnApplicationQuit()
	{
		foreach(var chunk in chunkStorage)
		{
			chunk.Value.OnDestroy();
		}
		blockTypesJobs.Dispose();
	}
}