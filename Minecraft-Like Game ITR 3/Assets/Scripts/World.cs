using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
	public static World Instance;

	[SerializeField] private        BlockTypes[]               blockTypes;
	public                          BlockTypes[]               BlockTypes => blockTypes;
	public                          NativeArray<BlockTypesJob> BlockTypesJobs;
	[field: SerializeField] public  Material                   Material { get; private set; }
	[SerializeField]        private int                        seed;
	[SerializeField]        private BiomeAttributes            biomeAttributes;
	public                          BiomeAttributesJob         BiomeAttributesJob { get; private set; }

	private Dictionary<int3, Chunk> ChunkStorage { get; } = new();

	private readonly List<int3> chunksToCreate = new();
	private readonly List<int3> activeChunks   = new();

	public  Transform PlayerTransform  { get; private set; }
	public  int3      PlayerChunkCoord { get; private set; }
	private int3      playerLastChunkCoord;
	private byte      lastViewDistance = VoxelData.ViewDistanceInChunks;

	public Bounds ChunkBound { get; private set; }

	private                        FastNoise worldGen;
	public                         IntPtr    WorldGenNodePtr;
	[field: SerializeField] public string    EncodedNodeTree { get; private set; }

	private void Awake()
	{
		Instance = this;

		Vector3 size = new(VoxelData.ChunkSize, VoxelData.ChunkSize, VoxelData.ChunkSize);
		ChunkBound = new Bounds(size / 2, size);

		BlockTypesJobs = new NativeArray<BlockTypesJob>(blockTypes.Length, Allocator.Persistent);

		for (var i = 0; i < blockTypes.Length; i++) BlockTypesJobs[i] = blockTypes[i].BlockTypeData;

		BiomeAttributesJob = biomeAttributes.BiomeData;

		PlayerTransform = GameObject.Find("PlayerCapsule").GetComponent<Transform>();
	}

	private void SetFastNoise()
	{
		worldGen = FastNoise.FromEncodedNodeTree(EncodedNodeTree);
		Assert.IsNotNull(worldGen, "worldGen is null, invalid encoded node tree");
		WorldGenNodePtr = worldGen.NodeHandlePtr;
	}

	private UniTask                    currentViewDistanceTask;

	private void Start()
	{
		Random.InitState(seed);
		SetFastNoise();
		RunCheckViewDistance();
	}

	private void Update()
	{
		PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);

		if (!PlayerChunkCoord.Equals(playerLastChunkCoord) || VoxelData.ViewDistanceInChunks != lastViewDistance)
		{
			lastViewDistance = VoxelData.ViewDistanceInChunks;
			RunCheckViewDistance();
		}

		if (chunksToCreate.Count <= 0) return;

		for (var i = 0; i < chunksToCreate.Count; i++)
		{
			if (!ChunkStorage[chunksToCreate[i]].IsScheduled)
				ChunkStorage[chunksToCreate[i]].Initialise();

			if (!ChunkStorage[chunksToCreate[i]].IsScheduled || !ChunkStorage[chunksToCreate[i]].IsMeshDataCompleted ||
			    !ChunkStorage[chunksToCreate[i]].VoxelMapPopulated) continue;

			ChunkStorage[chunksToCreate[i]].CreateMesh();
			chunksToCreate.RemoveAt(i);
		}
	}
	
	private void RunCheckViewDistance()
	{
		// Prevent overlapping tasks.
		if (!currentViewDistanceTask.Status.IsCompleted())
			return;

		currentViewDistanceTask = CheckViewDistanceAsync();
		Debug.Log($"Running CheckViewDistance: {currentViewDistanceTask.Status}");
	}
	
	private async UniTask CheckViewDistanceAsync()
	{
		int3       coord                  = GetChunkCoordFromVector3(PlayerTransform.position);
		List<int3> previouslyActiveChunks = new();
		previouslyActiveChunks.AddRange(activeChunks);
		activeChunks.Clear();

		playerLastChunkCoord = PlayerChunkCoord;

		List<int3> chunksToCheck = new();

		// Gather all potential chunks within view distance.
		for (var y = coord.y - VoxelData.ViewDistanceInChunks; y < coord.y + VoxelData.ViewDistanceInChunks; y++)
		for (var x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++)
		for (var z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++)
		{
			int3 chunkCoord = new(x, y, z);
			if (IsChunkInWorld(chunkCoord)) chunksToCheck.Add(chunkCoord);
		}

		// Sort chunks by distance from the player's current chunk position.
		chunksToCheck.Sort((a, b) =>
			                   math.distance(coord, a).CompareTo(math.distance(coord, b))
		                  );

		// Process chunks in batches to avoid frame freeze.
		const int batchSize = 8; // Number of chunks to process per batch.
		for (var i = 0; i < chunksToCheck.Count; i++)
		{
			int3 chunkCoord = chunksToCheck[i];

			if (!ChunkStorage.ContainsKey(chunkCoord))
				CreateNewChunk(chunkCoord.x, chunkCoord.y, chunkCoord.z);
			else if (!ChunkStorage[chunkCoord].IsActive) ChunkStorage[chunkCoord].IsActive = true;

			activeChunks.Add(chunkCoord);
			previouslyActiveChunks.Remove(chunkCoord);

			// Wait after processing a batch.
			if (i % batchSize == 0) await UniTask.Yield(); // Allow the game loop to continue.
		}

		// Disable chunks that are no longer within view distance.
		foreach (int3 chunk in previouslyActiveChunks) ChunkStorage[chunk].IsActive = false;

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
		return coord.x >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.x < VoxelData.WorldSizeInChunks * 0.5f &&
		       coord.y >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.y < VoxelData.WorldSizeInChunks * 0.5f &&
		       coord.z >= -(VoxelData.WorldSizeInChunks * 0.5f) && coord.z < VoxelData.WorldSizeInChunks * 0.5f;
	}

	public bool CheckForVoxel(Vector3 pos)
	{
		int3 thisChunk = new(math.floor(pos) / VoxelData.ChunkSize);

		if (!IsChunkInWorld(thisChunk)) return false;

		if (ChunkStorage.ContainsKey(thisChunk) && !ChunkStorage[thisChunk].IsUpdating)
			return blockTypes[ChunkStorage[thisChunk].GetVoxelFromGlobalVector3(pos)].BlockTypeData.IsSolid;

		return blockTypes
			       [WorldExtensions.GetVoxel(WorldGenNodePtr, pos.x, pos.y, pos.z, VoxelData.ChunkSize, BiomeAttributesJob.BiomeScale, BiomeAttributesJob.BiomeHeight, BiomeAttributesJob.SolidGroundHeight)]
		       .BlockTypeData.IsSolid;
	}

	private void OnApplicationQuit()
	{
		foreach (KeyValuePair<int3, Chunk> chunk in ChunkStorage) chunk.Value.OnDestroy();
		BlockTypesJobs.Dispose();
	}
}