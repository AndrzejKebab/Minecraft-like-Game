using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
	public static World Instance;

	[SerializeField] private BlockTypes[] blockTypes;
	public BlockTypes[] BlockTypes => blockTypes;
	public NativeArray<BlockTypesJob> BlockTypesJobs;
	[field:SerializeField] public Material Material { get; private set; }
	[SerializeField] private int seed;
	[SerializeField] private BiomeAttributes biomeAttributes;
	public BiomeAttributesJob BiomeAttributesJob { get; private set; }

	private Dictionary<int3, Chunk> ChunkStorage { get; } = new();

	private readonly List<int3> chunksToCreate = new();
	private readonly List<int3> activeChunks = new();

	public Transform PlayerTransform { get; private set; }
	public int3 PlayerChunkCoord { get; private set; }
	private int3 playerLastChunkCoord;
	private byte lastViewDistance = VoxelData.ViewDistanceInChunks;

	public Bounds ChunkBound { get; private set; }

	private void Awake()
	{
		Instance = this;

		Vector3 size = new(VoxelData.ChunkSize, VoxelData.ChunkSize, VoxelData.ChunkSize);
		ChunkBound = new Bounds(size / 2, size);

		BlockTypesJobs = new NativeArray<BlockTypesJob>(blockTypes.Length, Allocator.Persistent);

		for(var i = 0; i < blockTypes.Length; i++)
		{
			BlockTypesJobs[i] = blockTypes[i].BlockTypeData;
		}

		BiomeAttributesJob = biomeAttributes.BiomeData;

		PlayerTransform = GameObject.Find("PlayerCapsule").GetComponent<Transform>();
	}

	private void Start()
	{
		Random.InitState(seed);
		CheckViewDistance();
	}

	private void Update()
	{
		PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);
		if (!PlayerChunkCoord.Equals(playerLastChunkCoord) || VoxelData.ViewDistanceInChunks != lastViewDistance)
		{
			CheckViewDistance();
			lastViewDistance = VoxelData.ViewDistanceInChunks;
		}

		if (chunksToCreate.Count <= 0) return;
		for (var i = 0; i < chunksToCreate.Count; i++)
		{
			if (!ChunkStorage[chunksToCreate[i]].IsScheduled)
			{
				ChunkStorage[chunksToCreate[i]].Initialise();
			}

			//if (!chunkStorage[chunksToCreate[i]].VoxelMapPopulated)
			//{
			//	chunkStorage[chunksToCreate[i]].PopulateVoxelMap();
			//}

			//if (chunkStorage[chunksToCreate[i]].IsScheduled && chunkStorage[chunksToCreate[i]].IsVoxelMapCompleted)
			//{
			//	chunkStorage[chunksToCreate[i]].CreateMeshDataJob();
			//}

			if (!ChunkStorage[chunksToCreate[i]].IsScheduled || !ChunkStorage[chunksToCreate[i]].IsMeshDataCompleted ||
			    !ChunkStorage[chunksToCreate[i]].VoxelMapPopulated) continue;
			ChunkStorage[chunksToCreate[i]].CreateMesh();
			chunksToCreate.RemoveAt(i);
		}
	}

	private void CheckViewDistance()
	{
		var coord = GetChunkCoordFromVector3(PlayerTransform.position);
		List<int3> previouslyActiveChunks = new();
		previouslyActiveChunks.AddRange(activeChunks);
		activeChunks.Clear();
	
		playerLastChunkCoord = PlayerChunkCoord;
	
		for (var y = coord.y - (VoxelData.ViewDistanceInChunks / 4); y < coord.y + VoxelData.ViewDistanceInChunks; y++)
		{
			for (var x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++)
			{
				for(var z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++)
				{
					if (!IsChunkInWorld(new int3(x, y, z))) continue;
					if (!ChunkStorage.ContainsKey(new int3(x, y, z)))
					{
						CreateNewChunk(x, y, z);
					}
					else if (!ChunkStorage[new int3(x, y, z)].IsActive)
					{
						ChunkStorage[new int3(x, y, z)].IsActive = true;
					}
					activeChunks.Add(new int3(x, y, z));
					previouslyActiveChunks.Remove(new int3(x, y, z));
				}
			}
		}
	
		foreach (var chunk in previouslyActiveChunks)
		{
			ChunkStorage[new int3(chunk.x, chunk.y, chunk.z)].IsActive = false;
		}
		previouslyActiveChunks.Clear();
	}

	private void CreateNewChunk(int x, int y, int z)
	{
		ChunkStorage[new int3(x, y, z)] = new Chunk(new int3(x, y, z), this);
		chunksToCreate.Add(new int3(x, y, z));
	}

	private static int3 GetChunkCoordFromVector3(Vector3 pos)
	{
		var x = Mathf.FloorToInt(pos.x / VoxelData.ChunkSize);
		var y = Mathf.FloorToInt(pos.y / VoxelData.ChunkSize);
		var z = Mathf.FloorToInt(pos.z / VoxelData.ChunkSize);

		return new int3(x, y, z);
	}

	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		var x = Mathf.FloorToInt(pos.x / VoxelData.ChunkSize);
		var y = Mathf.FloorToInt(pos.y / VoxelData.ChunkSize);
		var z = Mathf.FloorToInt(pos.z / VoxelData.ChunkSize);

		return ChunkStorage[new int3(x, y, z)];
	}

	private static bool IsChunkInWorld(int3 coord)
	{
		return coord.x >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.x < (VoxelData.WorldSizeInChunks * 0.5f) &&
		       coord.y >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.y < (VoxelData.WorldSizeInChunks * 0.5f) &&
		       coord.z >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.z < (VoxelData.WorldSizeInChunks * 0.5f);
	}

	public bool CheckForVoxel(Vector3 pos)
	{
		int3 thisChunk = new(math.floor(pos) / VoxelData.ChunkSize);

		if (!IsChunkInWorld(thisChunk))
		{
			return false;
		}

		if (ChunkStorage.ContainsKey(thisChunk) && !ChunkStorage[thisChunk].IsUpdating)
		{
			return blockTypes[ChunkStorage[thisChunk].GetVoxelFromGlobalVector3(pos)].BlockTypeData.IsSolid;
		}

		return blockTypes[WorldExtensions.GetVoxel(pos.x ,pos.y, pos.z, VoxelData.ChunkSize, BiomeAttributesJob.BiomeScale, BiomeAttributesJob.BiomeHeight, BiomeAttributesJob.SolidGroundHeight)].BlockTypeData.IsSolid;
	}

	private void OnApplicationQuit()
	{
		foreach(KeyValuePair<int3, Chunk> chunk in ChunkStorage)
		{
			chunk.Value.OnDestroy();
		}
		BlockTypesJobs.Dispose();
	}
}