using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
	public static World Instance;

	[SerializeField]        private BlockTypes[]    blockTypes;
	[field: SerializeField] public  Material        Material { get; private set; }
	[SerializeField]        private int             seed;
	[SerializeField]        private BiomeAttributes biomeAttributes;
	[field: SerializeField] public  string          EncodedNodeTree { get; private set; }
	private readonly                List<int3>      activeChunks = [];

	private readonly       List<int3>                 chunksToCreate = [];
	[NonSerialized] public NativeArray<BlockTypesJob> BlockTypesJobs;

	private UniTask currentViewDistanceTask;
	private byte    lastViewDistance = VoxelData.ViewDistanceInChunks;
	private int3    playerLastChunkCoord;

	private FastNoise          worldGen;
	public  IntPtr             WorldGenNodePtr;
	public  BlockTypes[]       BlockTypes         => blockTypes;
	public  BiomeAttributesJob BiomeAttributesJob { get; private set; }

	private Dictionary<int3, Chunk> ChunkStorage { get; } = new();

	public Transform PlayerTransform  { get; private set; }
	public int3      PlayerChunkCoord { get; private set; }

	public Bounds ChunkBound { get; private set; }

	private ObjectPool<Chunk> ChunkPool { get; set; }

	private void Awake()
	{
		Instance = this;

		Vector3 size = new(VoxelData.CHUNK_SIZE, VoxelData.CHUNK_SIZE, VoxelData.CHUNK_SIZE);
		ChunkBound = new Bounds(size / 2, size);

		BlockTypesJobs = new NativeArray<BlockTypesJob>(blockTypes.Length, Allocator.Persistent);
		for (var i = 0; i < blockTypes.Length; i++) BlockTypesJobs[i] = blockTypes[i].BlockTypeData;

		BiomeAttributesJob = biomeAttributes.BiomeData;

		PlayerTransform = GameObject.Find("PlayerCapsule").GetComponent<Transform>();

		InitPool();
	}

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

			if (!ChunkStorage[chunksToCreate[i]].IsScheduled ||
			    !ChunkStorage[chunksToCreate[i]].IsMeshDataCompleted ||
			    !ChunkStorage[chunksToCreate[i]].VoxelMapPopulated) continue;

			ChunkStorage[chunksToCreate[i]].CreateMesh();
			chunksToCreate.RemoveAt(i);
		}
	}

	private void OnApplicationQuit()
	{
		foreach (KeyValuePair<int3, Chunk> pair in ChunkStorage)
			ChunkPool.Release(pair.Value);

		ChunkStorage.Clear();
		ChunkPool.Dispose();
		BlockTypesJobs.Dispose();
	}

	private void InitPool()
	{
		var viewDiam     = VoxelData.ViewDistanceInChunks * 2;
		var maxVisible   = viewDiam * viewDiam * viewDiam;
		var poolCapacity = maxVisible * 2;

		ChunkPool = new ObjectPool<Chunk>(
		                                  () => new Chunk(),
		                                  _ => { },
		                                  chunk => chunk.Release(),
		                                  chunk => chunk.OnDestroy(),
		                                  false,
		                                  maxVisible,
		                                  poolCapacity
		                                 );
	}

	private void SetFastNoise()
	{
		worldGen = FastNoise.FromEncodedNodeTree(EncodedNodeTree);
		Assert.IsNotNull(worldGen, "worldGen is null, invalid encoded node tree");
		WorldGenNodePtr = worldGen.NodeHandlePtr;
	}

	private void RunCheckViewDistance()
	{
		if (!currentViewDistanceTask.Status.IsCompleted())
			return;

		currentViewDistanceTask = CheckViewDistanceAsync();
		Debug.Log($"Running CheckViewDistance: {currentViewDistanceTask.Status}");
	}

	private async UniTask CheckViewDistanceAsync()
	{
		int3       coord                  = GetChunkCoordFromVector3(PlayerTransform.position);
		List<int3> previouslyActiveChunks = [];
		previouslyActiveChunks.AddRange(activeChunks);
		activeChunks.Clear();

		playerLastChunkCoord = PlayerChunkCoord;

		List<int3> chunksToCheck = [];

		for (var y = coord.y - VoxelData.ViewDistanceInChunks; y < coord.y + VoxelData.ViewDistanceInChunks; y++)
		for (var x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++)
		for (var z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++)
		{
			int3 chunkCoord = new(x, y, z);
			if (IsChunkInWorld(chunkCoord)) chunksToCheck.Add(chunkCoord);
		}

		chunksToCheck.Sort((a, b) =>
			                   math.distance(coord, a).CompareTo(math.distance(coord, b)));

		const int batchSize = 8;
		for (var i = 0; i < chunksToCheck.Count; i++)
		{
			int3 chunkCoord = chunksToCheck[i];

			if (!ChunkStorage.ContainsKey(chunkCoord))
				CreateNewChunk(chunkCoord);

			activeChunks.Add(chunkCoord);
			previouslyActiveChunks.Remove(chunkCoord);

			if (i % batchSize == 0) await UniTask.Yield();
		}

		foreach (int3 chunkCoord in previouslyActiveChunks)
		{
			if (!ChunkStorage.Remove(chunkCoord, out Chunk chunk)) continue;
			ChunkPool.Release(chunk);
		}

		previouslyActiveChunks.Clear();
	}

	private void CreateNewChunk(int3 coord)
	{
		Chunk chunk = ChunkPool.Get();
		chunk.Init(coord, this);
		ChunkStorage[coord] = chunk;
		chunksToCreate.Add(coord);
	}

	private static int3 GetChunkCoordFromVector3(Vector3 pos)
	{
		var x = Mathf.FloorToInt(pos.x / VoxelData.CHUNK_SIZE);
		var y = Mathf.FloorToInt(pos.y / VoxelData.CHUNK_SIZE);
		var z = Mathf.FloorToInt(pos.z / VoxelData.CHUNK_SIZE);
		return new int3(x, y, z);
	}

	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		var x = Mathf.FloorToInt(pos.x / VoxelData.CHUNK_SIZE);
		var y = Mathf.FloorToInt(pos.y / VoxelData.CHUNK_SIZE);
		var z = Mathf.FloorToInt(pos.z / VoxelData.CHUNK_SIZE);
		return ChunkStorage[new int3(x, y, z)];
	}

	public bool TryGetChunk(int3 coord, out Chunk chunk)
	{
		return ChunkStorage.TryGetValue(coord, out chunk);
	}

	private static bool IsChunkInWorld(int3 coord)
	{
		return coord.x >= -(VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f) && coord.x < VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f &&
		       coord.y >= -(VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f) && coord.y < VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f &&
		       coord.z >= -(VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f) && coord.z < VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f;
	}

	public bool CheckForVoxel(Vector3 pos)
	{
		int3 thisChunk = new(math.floor(pos) / VoxelData.CHUNK_SIZE);

		if (!IsChunkInWorld(thisChunk)) return false;

		if (ChunkStorage.ContainsKey(thisChunk) && !ChunkStorage[thisChunk].IsUpdating)
			return blockTypes[ChunkStorage[thisChunk].GetVoxelFromGlobalVector3(pos)].BlockTypeData.IsSolid;

		return blockTypes
			       [WorldExtensions.GetVoxel(WorldGenNodePtr, pos.x, pos.y, pos.z, VoxelData.CHUNK_SIZE,
			                                 BiomeAttributesJob.BiomeScale, BiomeAttributesJob.BiomeHeight,
			                                 BiomeAttributesJob.SolidGroundHeight)]
		       .BlockTypeData.IsSolid;
	}
}