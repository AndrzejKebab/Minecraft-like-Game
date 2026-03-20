using System;
using System.Collections.Generic;
using FastNoise2.Bindings;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using ZLinq;
using Random = UnityEngine.Random;
using ChunkPool = UnityEngine.Pool.ObjectPool<Chunk>;
using ColliderPool = UnityEngine.Pool.ObjectPool<UnityEngine.GameObject>;

public class World : MonoBehaviour
{
	#region Rendering

	private void DrawChunks()
	{
		foreach (Chunk chunk in chunkStorage.Select(pair => pair.Value).Where(chunk => chunk.IsActive && chunk.HasMesh))
			Graphics.DrawMesh(chunk.Mesh, chunk.ChunkPosition.ToVector3(), drawRotation, material, chunkLayer);
	}

	#endregion

	#region Chunk queue

	private void ProcessChunkQueue()
	{
		if (chunksToCreate.Count == 0) return;

		var initsThisFrame = 0;
		removeIndices.Clear();

		for (var i = 0; i < chunksToCreate.Count; i++)
		{
			int3 coord = chunksToCreate[i];

			if (!chunkStorage.TryGetValue(coord, out Chunk chunk))
			{
				removeIndices.Add(i);
				continue;
			}

			// Phase 1 — schedule the voxel populate job.
			if (!chunk.IsScheduled)
			{
				if (initsThisFrame >= maxChunkInitsPerFrame) continue;
				chunk.Initialise();
				initsThisFrame++;
				continue;
			}

			// Phase 2 — wait for voxel job to finish.
			if (!chunk.VoxelMapPopulated) continue;

			// Phase 3 — wait until every in-storage neighbor has its voxel map ready.
			// CheckVoxel returns true (cull) for neighbors outside storage, so those
			// are fine. We only wait for neighbors that exist but aren't populated yet.
			if (!AllStoredNeighborsMapped(coord)) continue;

			// Phase 4 — schedule mesh job once, then wait for it.
			if (!chunk.IsMeshScheduled)
			{
				chunk.ScheduleMeshDataJob();
				continue;
			}

			// Phase 5 — mesh job finished, apply geometry.
			if (!chunk.IsMeshDataCompleted) continue;

			chunk.CreateMesh();
			removeIndices.Add(i);
		}

		for (var i = removeIndices.Count - 1; i >= 0; i--)
			chunksToCreate.RemoveAt(removeIndices[i]);
	}

	/// <summary>
	/// Returns true when every chunk neighbor that exists in storage has its
	/// voxel map populated. Neighbors outside storage are outside view range —
	/// CheckVoxel culls those faces with return true, so they don't need to wait.
	/// </summary>
	private bool AllStoredNeighborsMapped(int3 coord)
	{
		for (int face = 0; face < 6; face++)
		{
			int3 neighborCoord = coord + VoxelData.FaceChecks[face];
			if (chunkStorage.TryGetValue(neighborCoord, out Chunk neighbor)
			    && !neighbor.VoxelMapPopulated)
				return false;
		}
		return true;
	}

	#endregion

	#region Collider bake queue

	private void ProcessColliderBakeQueue()
	{
		var baked = 0;
		while (pendingBakes.Count > 0 && baked < maxColliderBakesPerFrame)
		{
			Chunk chunk = pendingBakes.Dequeue();

			if (!chunk.HasMesh || chunk.Mesh.vertexCount == 0) continue;
			if (!IsChebyshevNear(chunk.Coord, PlayerChunkCoord, COLLIDER_RADIUS)) continue;

			Physics.BakeMesh(chunk.Mesh.GetEntityId(), false);
			AssignColliderImmediate(chunk);
			baked++;
		}
	}

	#endregion

	#region Constants & shared data

	private const int COLLIDER_RADIUS = 1;

	public static NativeArray<VertexAttributeDescriptor> Layout;

	private static readonly Quaternion drawRotation = Quaternion.identity;

	#endregion

	#region Inspector fields

	[SerializeField] private BlockTypes[]    blockTypes;
	[SerializeField] private Material        material;
	[SerializeField] private int             seed;
	[SerializeField] private BiomeAttributes biomeAttributes;
	[SerializeField] private string          encodedNodeTree;
	[SerializeField] private int             maxChunkInitsPerFrame    = 1;
	[SerializeField] private int             maxColliderBakesPerFrame = 1;

	#endregion

	#region Public API

	public BlockTypes[]       BlockTypes         => blockTypes;
	public BiomeAttributesJob BiomeAttributesJob { get; private set; }
	public Bounds             ChunkBound         { get; private set; }
	public Transform          PlayerTransform    { get; private set; }
	public int3               PlayerChunkCoord   { get; private set; }

	public int Seed => seed;

	public FastNoise WorldGen { get; private set; }

	[NonSerialized] public NativeArray<BlockTypesJob> BlockTypesJobs;

	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		return chunkStorage[WorldToChunkCoord(pos)];
	}

	public bool TryGetChunk(int3 coord, out Chunk chunk)
	{
		return chunkStorage.TryGetValue(coord, out chunk);
	}

	#endregion

	#region Private state

	private int       chunkLayer;
	private int3      playerLastChunkCoord;
	private byte      lastViewDistance = VoxelData.ViewDistanceInChunks;
	private FastNoise worldGen;

	private ChunkPool    chunkPool;
	private ColliderPool colliderGoPool;

	private readonly Dictionary<int3, Chunk>      chunkStorage    = new();
	private readonly Dictionary<int3, GameObject> activeColliders = new();

	private readonly List<int3> chunksToCreate = [];

	private readonly Queue<Chunk> pendingBakes = new();

	private readonly List<int>     removeIndices     = [];
	private readonly List<int3>    chunksToRelease   = [];
	private readonly List<int3>    collidersToReturn = [];
	private readonly HashSet<int3> desiredCoords     = [];

	#endregion

	#region Unity lifecycle

	private void Awake()
	{
		Layout = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Persistent)
	                                                                       {
		                                                                       [0] =
			                                                                       new
				                                                                       VertexAttributeDescriptor(VertexAttribute
						                                                                        .Position,
					                                                                        VertexAttributeFormat
						                                                                        .Float16, 4),
		                                                                       [1] =
			                                                                       new
				                                                                       VertexAttributeDescriptor(VertexAttribute
						                                                                        .Normal,
					                                                                        VertexAttributeFormat
						                                                                        .SNorm8, 4),
		                                                                       [2] =
			                                                                       new
				                                                                       VertexAttributeDescriptor(VertexAttribute
						                                                                        .Tangent,
					                                                                        VertexAttributeFormat
						                                                                        .UNorm8, 4),
		                                                                       [3] =
			                                                                       new
				                                                                       VertexAttributeDescriptor(VertexAttribute
						                                                                        .TexCoord0,
					                                                                        VertexAttributeFormat
						                                                                        .Float16, 2)
	                                                                       };
		const float size = VoxelData.CHUNK_SIZE;
		ChunkBound = new Bounds(
		                        new Vector3(size * 0.5f, size * 0.5f, size * 0.5f),
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
		PlayerChunkCoord = WorldToChunkCoord(PlayerTransform.position);

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
		ProcessColliderBakeQueue();
		DrawChunks();
	}

	private void OnApplicationQuit()
	{
		foreach (KeyValuePair<int3, Chunk> pair in chunkStorage) chunkPool.Release(pair.Value);
		foreach (KeyValuePair<int3, GameObject> pair in activeColliders) colliderGoPool.Release(pair.Value);

		chunkStorage.Clear();
		chunkPool.Dispose();

		activeColliders.Clear();
		colliderGoPool.Dispose();

		if (Layout.IsCreated) Layout.Dispose();
		BlockTypesJobs.Dispose();
	}

	#endregion

	#region View distance

	private void CheckViewDistance()
	{
		int3 center = WorldToChunkCoord(PlayerTransform.position);

		desiredCoords.Clear();
		for (var y = center.y - VoxelData.ViewDistanceInChunks; y < center.y + VoxelData.ViewDistanceInChunks; y++)
		for (var x = center.x - VoxelData.ViewDistanceInChunks; x < center.x + VoxelData.ViewDistanceInChunks; x++)
		for (var z = center.z - VoxelData.ViewDistanceInChunks; z < center.z + VoxelData.ViewDistanceInChunks; z++)
		{
			int3 c = new(x, y, z);
			if (IsChunkInWorld(c)) desiredCoords.Add(c);
		}

		chunksToRelease.Clear();
		foreach (int3 c in chunkStorage.Keys.Where(c => !desiredCoords.Contains(c)))
			chunksToRelease.Add(c);

		foreach (int3 c in chunksToRelease)
		{
			chunksToCreate.Remove(c);
			ReturnCollider(c);
			if (chunkStorage.Remove(c, out Chunk chunk))
				chunkPool.Release(chunk);
		}

		var anyAdded = false;
		foreach (int3 c in desiredCoords.Where(c => !chunkStorage.ContainsKey(c)))
		{
			CreateNewChunk(c);
			anyAdded = true;
		}

		if (anyAdded)
			chunksToCreate.Sort((a, b) => math.distance(center, a).CompareTo(math.distance(center, b)));
	}

	public void OnChunkMeshReady(Chunk chunk)
	{
		// Bake collider immediately — don't queue, to avoid the 1-2 frame gap
		// where Mesh.Clear() ran but the new collider isn't assigned yet.
		if (IsChebyshevNear(chunk.Coord, PlayerChunkCoord, COLLIDER_RADIUS)
		    && chunk.Mesh.vertexCount > 0)
		{
			Physics.BakeMesh(chunk.Mesh.GetEntityId(), false);
			AssignColliderImmediate(chunk);
		}
	}

	#endregion

	#region Colliders

	private void AssignColliderImmediate(Chunk chunk)
	{
		if (activeColliders.TryGetValue(chunk.Coord, out GameObject existing))
		{
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

	private void UpdateNearbyColliders(int3 center)
	{
		collidersToReturn.Clear();
		foreach (int3 c in activeColliders.Keys.Where(c => !IsChebyshevNear(c, center, COLLIDER_RADIUS)))
			collidersToReturn.Add(c);

		foreach (int3 c in collidersToReturn)
			ReturnCollider(c);

		for (var dy = -COLLIDER_RADIUS; dy <= COLLIDER_RADIUS; dy++)
		for (var dx = -COLLIDER_RADIUS; dx <= COLLIDER_RADIUS; dx++)
		for (var dz = -COLLIDER_RADIUS; dz <= COLLIDER_RADIUS; dz++)
		{
			int3 coord = center + new int3(dx, dy, dz);
			if (!activeColliders.ContainsKey(coord)
			    && chunkStorage.TryGetValue(coord, out Chunk chunk)
			    && chunk.HasMesh)
				pendingBakes.Enqueue(chunk);
		}
	}

	private static bool IsChebyshevNear(int3 a, int3 b, int radius)
	{
		int3 d = math.abs(a - b);
		return d.x <= radius && d.y <= radius && d.z <= radius;
	}

	#endregion

	#region Pool initialisation

	private void InitChunkPool()
	{
		var viewDiam   = VoxelData.ViewDistanceInChunks * 2;
		var maxVisible = viewDiam * viewDiam * viewDiam;

		chunkPool = new ChunkPool(
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

		colliderGoPool = new ColliderPool(
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
		                                  capacity * 2);
	}

	#endregion

	#region Helpers

	private void SetFastNoise()
	{
		worldGen = FastNoise.FromEncodedNodeTree(encodedNodeTree);
		WorldGen = worldGen;
	}

	private void CreateNewChunk(int3 coord)
	{
		Chunk chunk = chunkPool.Get();
		chunk.Init(coord, this);
		chunkStorage[coord] = chunk;
		chunksToCreate.Add(coord);
	}

	private static int3 WorldToChunkCoord(Vector3 pos)
	{
		return new int3(
		                Mathf.FloorToInt(pos.x / VoxelData.CHUNK_SIZE),
		                Mathf.FloorToInt(pos.y / VoxelData.CHUNK_SIZE),
		                Mathf.FloorToInt(pos.z / VoxelData.CHUNK_SIZE));
	}

	private static bool IsChunkInWorld(int3 coord)
	{
		const float half = VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f;
		return coord.x >= -half && coord.x < half &&
		       coord.y >= -half && coord.y < half &&
		       coord.z >= -half && coord.z < half;
	}

	#endregion
}