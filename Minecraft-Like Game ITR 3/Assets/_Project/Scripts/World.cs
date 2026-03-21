using System;
using System.Collections.Generic;
using System.Linq;
using FastNoise2.Bindings;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using ZLinq;
using Random = UnityEngine.Random;
using ChunkPool = UnityEngine.Pool.ObjectPool<Chunk>;
using ColliderPool = UnityEngine.Pool.ObjectPool<UnityEngine.GameObject>;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class World : MonoBehaviour
{
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
	private readonly HashSet<int3> desiredCoords  = [];
	/// <summary>Subset of desiredCoords within render distance — only these get a mesh job.</summary>
	private readonly HashSet<int3> renderCoords   = [];

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
	
	#region Rendering

	private void DrawChunks()
	{
		foreach (Chunk chunk in chunkStorage.Select(pair => pair.Value).Where(chunk => chunk.IsActive && chunk.HasMesh))
		{
			int3 chunkChunkPosition = chunk.ChunkPosition;
			Graphics.DrawMesh(chunk.Mesh, chunkChunkPosition.ToVector3(), drawRotation, material, chunkLayer);
		}
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
			// Outer-ring chunks (populate-only) bypass the per-frame cap: they are
			// cheap (voxel job only, no mesh) and render-ring edge chunks are blocked
			// at Phase 4 until all their outer-ring neighbors are populated.
			// Capping outer-ring inits creates a stall that prevents those chunks rendering.
			if (!chunk.IsScheduled)
			{
				bool isOuterRing = !renderCoords.Contains(coord);
				if (!isOuterRing && initsThisFrame >= maxChunkInitsPerFrame) continue;
				chunk.Initialise();
				if (!isOuterRing) initsThisFrame++;
				continue;
			}

			// Phase 2 — wait for voxel job to finish.
			if (!chunk.VoxelMapPopulated) continue;

			// Phase 3 — outer-ring (populate-only) chunks are done as soon as their
			// voxel map is ready. They need no mesh — they exist only as neighbor data.
			if (!renderCoords.Contains(coord))
			{
				removeIndices.Add(i);
				continue;
			}

			// Phase 4 — render-ring: wait until every in-storage neighbor has its
			// voxel map populated before scheduling the mesh job.
			if (!AllStoredNeighborsMapped(coord)) continue;

			// Phase 5 — schedule mesh job once.
			if (!chunk.IsMeshScheduled)
			{
				chunk.ScheduleMeshDataJob();
				continue;
			}

			// Phase 6 — mesh job finished, apply geometry.
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
		for (var face = 0; face < 6; face++)
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
			int3 chunkCoord       = chunk.Coord;
			int3 playerChunkCoord = PlayerChunkCoord;
			if (!IsChebyshevNear(ref chunkCoord, ref playerChunkCoord, COLLIDER_RADIUS)) continue;

			Physics.BakeMesh(chunk.Mesh.GetEntityId(), false);
			AssignColliderImmediate(chunk);
			baked++;
		}
	}

	#endregion
	
	#region View distance

	private void CheckViewDistance()
	{
		int3 center      = WorldToChunkCoord(PlayerTransform.position);
		int  renderDist  = VoxelData.ViewDistanceInChunks;
		int  populateDist = renderDist + 1; // one extra ring for neighbor voxel data

		// Build two sets: all chunks to keep in storage, and the inner render-only set.
		desiredCoords.Clear();
		renderCoords.Clear();
		for (var y = center.y - populateDist; y < center.y + populateDist; y++)
		for (var x = center.x - populateDist; x < center.x + populateDist; x++)
		for (var z = center.z - populateDist; z < center.z + populateDist; z++)
		{
			int3 c = new(x, y, z);
			if (!IsChunkInWorld(ref c)) continue;
			desiredCoords.Add(c);
			// Only chunks within render distance get a mesh.
			if (math.abs(x - center.x) < renderDist &&
			    math.abs(y - center.y) < renderDist &&
			    math.abs(z - center.z) < renderDist)
				renderCoords.Add(c);
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
		foreach (int3 c in desiredCoords)
		{
			if (!chunkStorage.ContainsKey(c))
			{
				CreateNewChunk(c);
				anyAdded = true;
			}
			// Chunk exists but was in the outer ring (no mesh) and has now entered
			// render range — re-queue it so it gets a mesh job this session.
			else if (renderCoords.Contains(c)
			      && chunkStorage.TryGetValue(c, out Chunk existing)
			      && existing.VoxelMapPopulated
			      && !existing.HasMesh
			      && !existing.IsMeshScheduled
			      && !chunksToCreate.Contains(c))
			{
				chunksToCreate.Add(c);
				anyAdded = true;
			}
		}

		if (anyAdded)
			chunksToCreate.Sort((a, b) => math.distance(center, a).CompareTo(math.distance(center, b)));
	}

	public void OnChunkMeshReady(Chunk chunk)
	{
		// Bake collider immediately — don't queue, to avoid the 1-2 frame gap
		// where Mesh.Clear() ran but the new collider isn't assigned yet.
		int3 chunkCoord       = chunk.Coord;
		int3 playerChunkCoord = PlayerChunkCoord;
		if (!IsChebyshevNear(ref chunkCoord, ref playerChunkCoord, COLLIDER_RADIUS)
		    || chunk.Mesh.vertexCount <= 0) return;
		Physics.BakeMesh(chunk.Mesh.GetEntityId(), false);
		AssignColliderImmediate(chunk);
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

		int3 chunkChunkPosition = chunk.ChunkPosition;
		GameObject go = colliderGoPool.Get();
		go.name                                    = $"ChunkCollider: {chunk.Coord}";
		go.transform.position                      = chunkChunkPosition.ToVector3();
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
		Func<int3, bool> predicate = c => !IsChebyshevNear(ref c, ref center, COLLIDER_RADIUS);
		foreach (int3 c in activeColliders.Keys.Where(predicate))
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
	
	[BurstCompile]
	private static bool IsChebyshevNear(ref int3 a, ref int3 b, int radius)
	{
		int3 d = math.abs(a - b);
		return d.x <= radius && d.y <= radius && d.z <= radius;
	}

	#endregion

	#region Pool initialisation

	private void InitChunkPool()
	{
		var viewDiam   = (VoxelData.ViewDistanceInChunks + 1) * 2; // +1 for the populate-only outer ring
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
	
	[BurstCompile]
	private static bool IsChunkInWorld(ref int3 coord)
	{
		const float half = VoxelData.WORLD_SIZE_IN_CHUNKS * 0.5f;
		return coord.x >= -half && coord.x < half &&
		       coord.y >= -half && coord.y < half &&
		       coord.z >= -half && coord.z < half;
	}

	#endregion
}