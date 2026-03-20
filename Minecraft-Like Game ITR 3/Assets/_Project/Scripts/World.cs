using System;
using System.Collections.Generic;
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
	[NonSerialized]         public  NativeArray<BlockTypesJob> BlockTypesJobs;
	[field: SerializeField] public  Material                   Material { get; private set; }
	[SerializeField]        private int                        seed;
	[SerializeField]        private BiomeAttributes            biomeAttributes;
	public                          BiomeAttributesJob         BiomeAttributesJob { get; private set; }

	private Dictionary<int3, Chunk> ChunkStorage { get; } = new();

	// Coords whose chunks still need Initialise() / CreateMesh() this session.
	// Kept sorted nearest-first so ProcessChunkQueue always spends its per-frame
	// budget on the closest pending chunks.
	private readonly List<int3> chunksToCreate = [];

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

	/// <summary>
	///     Max new chunk jobs submitted per Update tick.
	///     Higher = faster world load, lower = smoother framerate. Tune in the Inspector.
	/// </summary>
	[SerializeField] private int maxChunkInitsPerFrame = 2;

	private                        FastNoise worldGen;
	public                         IntPtr    WorldGenNodePtr;
	[field: SerializeField] public string    EncodedNodeTree { get; private set; }

	// -------------------------------------------------------------------------
	// Unity lifecycle
	// -------------------------------------------------------------------------

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

	private void Start()
	{
		Random.InitState(seed);
		SetFastNoise();
		CheckViewDistance();
	}

	private void Update()
	{
		PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);

		var chunkChanged    = !PlayerChunkCoord.Equals(playerLastChunkCoord);
		var distanceChanged = VoxelData.ViewDistanceInChunks != lastViewDistance;

		if (chunkChanged || distanceChanged)
		{
			lastViewDistance = VoxelData.ViewDistanceInChunks;
			CheckViewDistance();

			if (chunkChanged)
			{
				playerLastChunkCoord = PlayerChunkCoord;
				UpdateNearbyColliders(PlayerChunkCoord);
			}
		}

		ProcessChunkQueue();

		foreach (KeyValuePair<int3, Chunk> pair in ChunkStorage)
		{
			Chunk chunk = pair.Value;
			if (chunk.IsActive && chunk.HasMesh)
				Graphics.DrawMesh(chunk.Mesh, chunk.ChunkPosition.ToVector3(), identity, Material, chunkLayer);
		}
	}

	private void OnApplicationQuit()
	{
		foreach (KeyValuePair<int3, Chunk> pair in ChunkStorage)
			ChunkPool.Release(pair.Value);

		ChunkStorage.Clear();
		ChunkPool.Dispose();

		foreach (KeyValuePair<int3, GameObject> pair in activeColliders)
			colliderGoPool.Release(pair.Value);

		activeColliders.Clear();
		colliderGoPool.Dispose();
		BlockTypesJobs.Dispose();
	}

	// -------------------------------------------------------------------------
	// Chunk queue — nearest-first initialisation, deferred removal
	// -------------------------------------------------------------------------

	private void ProcessChunkQueue()
	{
		if (chunksToCreate.Count == 0) return;

		var initsThisFrame = 0;
		var toRemove       = new List<int>();

		// Forward iteration — index 0 is always the nearest chunk.
		// The per-frame cap ensures we never flood the job system in one tick.
		for (var i = 0; i < chunksToCreate.Count; i++)
		{
			int3 coord = chunksToCreate[i];

			// Chunk may have been released (player moved away) before we got to it.
			if (!ChunkStorage.TryGetValue(coord, out Chunk chunk))
			{
				toRemove.Add(i);
				continue;
			}

			if (!chunk.IsScheduled)
			{
				if (initsThisFrame >= maxChunkInitsPerFrame) continue;
				chunk.Initialise();
				initsThisFrame++;
				continue;
			}

			if (!chunk.IsMeshDataCompleted || !chunk.VoxelMapPopulated) continue;

			chunk.CreateMesh(); // calls OnChunkMeshReady → AssignCollider if in range
			toRemove.Add(i);
		}

		// Remove backwards so earlier indices stay valid as we delete later ones.
		for (var i = toRemove.Count - 1; i >= 0; i--)
			chunksToCreate.RemoveAt(toRemove[i]);
	}

	// -------------------------------------------------------------------------
	// View distance — ChunkStorage is the single source of truth
	// -------------------------------------------------------------------------

	/// <summary>
	///     Fully synchronous — no async, no cancellation tokens, no yields.
	///     <para>
	///         The previous async design caused permanent mesh holes: if the task was
	///         cancelled mid-loop, newly visible chunks were never added to ChunkStorage
	///         so ProcessChunkQueue never initialised them (only editing a block fixed them).
	///         CreateNewChunk is just a dictionary insert — fast even for thousands of coords.
	///         The slow work (job scheduling, mesh building) is rate-limited by ProcessChunkQueue.
	///     </para>
	/// </summary>
	private void CheckViewDistance()
	{
		int3 center = GetChunkCoordFromVector3(PlayerTransform.position);

		// Build the desired set for the current player position.
		var desired = new HashSet<int3>();
		for (var y = center.y - VoxelData.ViewDistanceInChunks; y < center.y + VoxelData.ViewDistanceInChunks; y++)
		for (var x = center.x - VoxelData.ViewDistanceInChunks; x < center.x + VoxelData.ViewDistanceInChunks; x++)
		for (var z = center.z - VoxelData.ViewDistanceInChunks; z < center.z + VoxelData.ViewDistanceInChunks; z++)
		{
			int3 c = new(x, y, z);
			if (IsChunkInWorld(c)) desired.Add(c);
		}

		// Release chunks that are no longer in range.
		var toRelease = new List<int3>();
		foreach (int3 c in ChunkStorage.Keys)
			if (!desired.Contains(c))
				toRelease.Add(c);

		foreach (int3 c in toRelease)
		{
			chunksToCreate.Remove(c);
			ReturnCollider(c);
			if (ChunkStorage.Remove(c, out Chunk chunk))
				ChunkPool.Release(chunk);
		}

		// Register all newly visible chunks immediately (just a dict insert each).
		var anyAdded = false;
		foreach (int3 c in desired)
		{
			if (ChunkStorage.ContainsKey(c)) continue;
			CreateNewChunk(c);
			anyAdded = true;
		}

		// Re-sort the entire queue nearest-first after appending.
		// New coords land at the end of chunksToCreate; without re-sorting, old
		// in-progress chunks at index 0 would eat the entire per-frame init budget
		// every frame and the newly visible nearby chunks would be permanently starved.
		if (anyAdded)
			chunksToCreate.Sort((a, b) =>
				                    math.distance(center, a).CompareTo(math.distance(center, b)));
	}

	// -------------------------------------------------------------------------
	// Chunk mesh callback — called by Chunk.CreateMesh() on the main thread
	// -------------------------------------------------------------------------

	public void OnChunkMeshReady(Chunk chunk)
	{
		if (IsChebyshevNear(chunk.Coord, PlayerChunkCoord, COLLIDER_RADIUS))
			AssignCollider(chunk);
	}

	// -------------------------------------------------------------------------
	// Collider pool — Physics.BakeMesh, no MeshCollider on chunk GameObjects
	// -------------------------------------------------------------------------

	private void InitChunkPool()
	{
		var viewDiam   = VoxelData.ViewDistanceInChunks * 2;
		var maxVisible = viewDiam * viewDiam * viewDiam;

		ChunkPool = new ObjectPool<Chunk>(
		                                  () => new Chunk(),
		                                  _ => { },
		                                  chunk => chunk.Release(),
		                                  chunk => chunk.OnDestroy(),
		                                  false,
		                                  maxVisible,
		                                  maxVisible * 2);
	}

	private void InitColliderPool()
	{
		const int diameter = COLLIDER_RADIUS * 2 + 1;
		const int capacity = diameter * diameter * diameter;

		colliderGoPool = new ObjectPool<GameObject>(
		                                            () =>
		                                            {
			                                            var go = new GameObject("ChunkCollider");
			                                            go.transform.SetParent(transform);
			                                            go.AddComponent<MeshCollider>();
			                                            go.layer = chunkLayer;
			                                            go.SetActive(false);
			                                            return go;
		                                            },
		                                            go => go.SetActive(true),
		                                            go =>
		                                            {
			                                            go.GetComponent<MeshCollider>().sharedMesh = null;
			                                            go.SetActive(false);
		                                            },
		                                            Destroy,
		                                            false,
		                                            capacity,
		                                            capacity);
	}

	/// <summary>
	///     Bakes physics for <paramref name="chunk" /> and assigns a collider GO.
	///     If this coord already has a collider (e.g. after EditVoxel), updates the mesh in-place.
	/// </summary>
	private void AssignCollider(Chunk chunk)
	{
		if (chunk.Mesh.vertexCount == 0) return;

		// BakeMesh before assigning sharedMesh to avoid a one-frame stale-mesh physics step.
		Physics.BakeMesh(chunk.Mesh.GetInstanceID(), false);

		if (activeColliders.TryGetValue(chunk.Coord, out GameObject existing))
		{
			// Coord already has a collider — just swap the mesh (e.g. after block edit).
			existing.GetComponent<MeshCollider>().sharedMesh = chunk.Mesh;
			return;
		}

		GameObject go = colliderGoPool.Get();
		go.name                                    = $"ChunkCollider: {chunk.Coord}";
		go.transform.position                      = chunk.ChunkPosition.ToVector3();
		go.GetComponent<MeshCollider>().sharedMesh = chunk.Mesh;
		activeColliders[chunk.Coord]               = go;
	}

	private void ReturnCollider(int3 coord)
	{
		if (!activeColliders.Remove(coord, out GameObject go)) return;
		colliderGoPool.Release(go);
	}

	/// <summary>
	///     Called whenever the player crosses a chunk boundary.
	///     Returns colliders that drifted out of range; assigns any in-range chunk
	///     that now has a mesh but no collider yet.
	/// </summary>
	private void UpdateNearbyColliders(int3 center)
	{
		var toReturn = new List<int3>();
		foreach (int3 c in activeColliders.Keys)
			if (!IsChebyshevNear(c, center, COLLIDER_RADIUS))
				toReturn.Add(c);

		foreach (int3 c in toReturn)
			ReturnCollider(c);

		for (var dy = -COLLIDER_RADIUS; dy <= COLLIDER_RADIUS; dy++)
		for (var dx = -COLLIDER_RADIUS; dx <= COLLIDER_RADIUS; dx++)
		for (var dz = -COLLIDER_RADIUS; dz <= COLLIDER_RADIUS; dz++)
		{
			int3 coord = center + new int3(dx, dy, dz);
			if (!activeColliders.ContainsKey(coord)
			    && ChunkStorage.TryGetValue(coord, out Chunk chunk)
			    && chunk.HasMesh)
				AssignCollider(chunk);
		}
	}

	private static bool IsChebyshevNear(int3 a, int3 b, int radius)
	{
		int3 d = math.abs(a - b);
		return d.x <= radius && d.y <= radius && d.z <= radius;
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private void SetFastNoise()
	{
		worldGen = FastNoise.FromEncodedNodeTree(EncodedNodeTree);
		Assert.IsNotNull(worldGen, "worldGen is null, invalid encoded node tree");
		WorldGenNodePtr = worldGen.NodeHandlePtr;
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

	public bool TryGetChunk(int3 coord, out Chunk chunk)
	{
		return ChunkStorage.TryGetValue(coord, out chunk);
	}

	private static bool IsChunkInWorld(int3 coord)
	{
		const float worldSize = VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f;
		return coord.x >= -worldSize && coord.x < worldSize &&
		       coord.y >= -worldSize && coord.y < worldSize &&
		       coord.z >= -worldSize && coord.z < worldSize;
	}
}