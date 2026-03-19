using System;
using System.Collections.Generic;
using System.Linq;
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

	[SerializeField] private        BlockTypes[]               blockTypes;
	public                          BlockTypes[]               BlockTypes => blockTypes;
	[NonSerialized] public          NativeArray<BlockTypesJob> BlockTypesJobs;
	[field: SerializeField] public  Material                   Material { get; private set; }
	[SerializeField]        private int                        seed;
	[SerializeField]        private BiomeAttributes            biomeAttributes;
	public                          BiomeAttributesJob         BiomeAttributesJob { get; private set; }

	private Dictionary<int3, Chunk> ChunkStorage { get; } = new();

	private readonly List<int3> chunksToCreate = [];
	private readonly List<int3> activeChunks   = [];

	public  Transform PlayerTransform  { get; private set; }
	public  int3      PlayerChunkCoord { get; private set; }
	private int3      playerLastChunkCoord;
	private byte      lastViewDistance = VoxelData.ViewDistanceInChunks;

	public Bounds ChunkBound { get; private set; }

	private                 int        chunkLayer;
	private static readonly Quaternion identity = Quaternion.identity;

	private ObjectPool<Chunk> ChunkPool { get; set; }
	
	private const int COLLIDER_RADIUS = 1;
	
	private ObjectPool<GameObject> colliderGoPool;

	private readonly Dictionary<int3, GameObject> activeColliders = new();

	private FastNoise worldGen;
	public  IntPtr    WorldGenNodePtr;
	[field: SerializeField] public string EncodedNodeTree { get; private set; }
	
	private void Awake()
	{
		Instance = this;

		const float size = VoxelData.CHUNK_SIZE;
		ChunkBound = new Bounds(new Vector3(size * 0.5f, size * 0.5f, size * 0.5f),
		                        new Vector3(size, size, size));

		BlockTypesJobs = new NativeArray<BlockTypesJob>(blockTypes.Length, Allocator.Persistent);
		for (var i = 0; i < blockTypes.Length; i++)
			BlockTypesJobs[i] = blockTypes[i].BlockTypeData;

		BiomeAttributesJob = biomeAttributes.BiomeData;
		PlayerTransform    = GameObject.Find("PlayerCapsule").GetComponent<Transform>();
		chunkLayer         = LayerMask.NameToLayer("Chunk");

		InitChunkPool();
		InitColliderPool();
	}

	private void InitChunkPool()
	{
		var viewDiam   = VoxelData.ViewDistanceInChunks * 2;
		var maxVisible = viewDiam * viewDiam * viewDiam;

		ChunkPool = new ObjectPool<Chunk>(
			createFunc:      ()    => new Chunk(),
			actionOnGet:     _     => { },
			actionOnRelease: chunk => chunk.Release(),
			actionOnDestroy: chunk => chunk.OnDestroy(),
			collectionCheck: false,
			defaultCapacity: maxVisible,
			maxSize:         maxVisible * 2);
	}

	private void InitColliderPool()
	{
		const int diameter = COLLIDER_RADIUS * 2 + 1;
		const int capacity = diameter * diameter * diameter;

		colliderGoPool = new ObjectPool<GameObject>(
			createFunc: () =>
			{
				var go = new GameObject("ChunkCollider");
				go.transform.SetParent(transform);
				go.AddComponent<MeshCollider>();
				go.layer = chunkLayer;
				go.SetActive(false);
				return go;
			},
			actionOnGet:     go => go.SetActive(true),
			actionOnRelease: go =>
			{
				go.GetComponent<MeshCollider>().sharedMesh = null;
				go.SetActive(false);
			},
			actionOnDestroy: Destroy,
			collectionCheck: false,
			defaultCapacity: capacity,
			maxSize:         capacity);
	}

	private void SetFastNoise()
	{
		worldGen = FastNoise.FromEncodedNodeTree(EncodedNodeTree);
		Assert.IsNotNull(worldGen, "worldGen is null, invalid encoded node tree");
		WorldGenNodePtr = worldGen.NodeHandlePtr;
	}

	private UniTask currentViewDistanceTask;

	private void Start()
	{
		Random.InitState(seed);
		SetFastNoise();
		RunCheckViewDistance();
	}

	private void Update()
	{
		PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);

		var chunkChanged    = !PlayerChunkCoord.Equals(playerLastChunkCoord);
		var distanceChanged = VoxelData.ViewDistanceInChunks != lastViewDistance;

		if (chunkChanged || distanceChanged)
		{
			lastViewDistance = VoxelData.ViewDistanceInChunks;
			RunCheckViewDistance();
			
			if (chunkChanged)
			{
				playerLastChunkCoord = PlayerChunkCoord;
				UpdateNearbyColliders(PlayerChunkCoord);
			}
		}

		for (var i = chunksToCreate.Count - 1; i >= 0; i--)
		{
			Chunk chunk = ChunkStorage[chunksToCreate[i]];

			if (!chunk.IsScheduled)
				chunk.Initialise();

			if (!chunk.IsScheduled || !chunk.IsMeshDataCompleted || !chunk.VoxelMapPopulated)
				continue;

			chunk.CreateMesh();
			chunksToCreate.RemoveAt(i);
		}
		
		foreach (Chunk chunk in ChunkStorage.Select(p => p.Value)
		                                     .Where(c => c.IsActive && c.HasMesh))
		{
			Graphics.DrawMesh(chunk.Mesh, chunk.ChunkPosition.ToVector3(), identity, Material, chunkLayer);
		}
	}
	
	private void RunCheckViewDistance()
	{
		if (!currentViewDistanceTask.Status.IsCompleted()) return;
		currentViewDistanceTask = CheckViewDistanceAsync();
	}

	private async UniTask CheckViewDistanceAsync()
	{
		int3       coord                  = GetChunkCoordFromVector3(PlayerTransform.position);
		List<int3> previouslyActiveChunks = new(activeChunks);
		activeChunks.Clear();

		var chunksToCheck = new List<int3>();

		for (var y = coord.y - VoxelData.ViewDistanceInChunks; y < coord.y + VoxelData.ViewDistanceInChunks; y++)
		for (var x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++)
		for (var z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++)
		{
			int3 c = new(x, y, z);
			if (IsChunkInWorld(c)) chunksToCheck.Add(c);
		}

		chunksToCheck.Sort((a, b) => math.distance(coord, a).CompareTo(math.distance(coord, b)));

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

		foreach (int3 c in previouslyActiveChunks)
		{
			if (!ChunkStorage.TryGetValue(c, out Chunk chunk)) continue;
			ReturnCollider(c);
			ChunkStorage.Remove(c);
			ChunkPool.Release(chunk);
		}

		previouslyActiveChunks.Clear();
	}
	
	public void OnChunkMeshReady(Chunk chunk)
	{
		if (IsChebyshevNear(chunk.Coord, PlayerChunkCoord, COLLIDER_RADIUS))
			AssignCollider(chunk);
	}
	
	private void UpdateNearbyColliders(int3 center)
	{
		var toReturn = new List<int3>(activeColliders.Keys);
		foreach (int3 c in toReturn.Where(c => !IsChebyshevNear(c, center, COLLIDER_RADIUS)))
		{
			ReturnCollider(c);
		}

		for (var dy = -COLLIDER_RADIUS; dy <= COLLIDER_RADIUS; dy++)
		for (var dx = -COLLIDER_RADIUS; dx <= COLLIDER_RADIUS; dx++)
		for (var dz = -COLLIDER_RADIUS; dz <= COLLIDER_RADIUS; dz++)
		{
			int3 coord = center + new int3(dx, dy, dz);
			if (!ChunkStorage.TryGetValue(coord, out Chunk chunk)) continue;
			if (!chunk.HasMesh) continue;
			AssignCollider(chunk);
		}
	}
	
	private void AssignCollider(Chunk chunk)
	{
		Physics.BakeMesh(chunk.Mesh.GetEntityId(), false);
		
		if (activeColliders.TryGetValue(chunk.Coord, out GameObject existing))
		{
			existing.GetComponent<MeshCollider>().sharedMesh = chunk.Mesh;
			return;
		}

		GameObject go = colliderGoPool.Get();
		go.name                                            = $"ChunkCollider: {chunk.Coord}";
		go.transform.position                              = chunk.ChunkPosition.ToVector3();
		go.GetComponent<MeshCollider>().sharedMesh         = chunk.Mesh;

		activeColliders[chunk.Coord] = go;
	}

	private void ReturnCollider(int3 coord)
	{
		if (!activeColliders.TryGetValue(coord, out GameObject go)) return;
		colliderGoPool.Release(go);
		activeColliders.Remove(coord);
	}

	private static bool IsChebyshevNear(int3 a, int3 b, int radius)
	{
		int3 d = math.abs(a - b);
		return d.x <= radius && d.y <= radius && d.z <= radius;
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
		return new int3(
			Mathf.FloorToInt(pos.x / VoxelData.CHUNK_SIZE),
			Mathf.FloorToInt(pos.y / VoxelData.CHUNK_SIZE),
			Mathf.FloorToInt(pos.z / VoxelData.CHUNK_SIZE));
	}

	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		return ChunkStorage[new int3(
			Mathf.FloorToInt(pos.x / VoxelData.CHUNK_SIZE),
			Mathf.FloorToInt(pos.y / VoxelData.CHUNK_SIZE),
			Mathf.FloorToInt(pos.z / VoxelData.CHUNK_SIZE))];
	}

	public bool TryGetChunk(int3 coord, out Chunk chunk) =>
		ChunkStorage.TryGetValue(coord, out chunk);

	private static bool IsChunkInWorld(int3 coord)
	{
		const float worldSize = VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f;
		return coord.x >= -worldSize && coord.x < worldSize &&
		       coord.y >= -worldSize && coord.y < worldSize &&
		       coord.z >= -worldSize && coord.z < worldSize;
	}

	private void OnApplicationQuit()
	{
		foreach (KeyValuePair<int3, Chunk> pair in ChunkStorage)
			ChunkPool.Release(pair.Value);

		ChunkStorage.Clear();
		ChunkPool.Dispose();
		colliderGoPool.Dispose();
		BlockTypesJobs.Dispose();
	}
}