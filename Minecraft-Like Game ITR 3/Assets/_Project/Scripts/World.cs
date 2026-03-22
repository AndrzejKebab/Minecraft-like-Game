using System;
using System.Collections.Generic;
using FastNoise2.Bindings;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Unity.Runtime.PriorityQueue;
using ZLinq;
using Random = UnityEngine.Random;
using ColliderPool = UnityEngine.Pool.ObjectPool<UnityEngine.GameObject>;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class World : MonoBehaviour
{
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

	#region Pool initialisation (colliders)

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

	#region Constants & shared data

	private const int COLLIDER_RADIUS = 1;

	private static readonly int verticesPropertyId      = Shader.PropertyToID("vertices");
	private static readonly int chunkPositionPropertyId = Shader.PropertyToID("uChunkPosition");

	private const           float   SIZE    = VoxelData.CHUNK_SIZE;
	private static readonly Vector3 extents = new(SIZE, SIZE, SIZE);
	private static readonly Vector3 half    = extents * 0.5f;

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

	private Bounds ChunkBound { get; } = new(
	                                         new Vector3(SIZE * 0.5f, SIZE * 0.5f, SIZE * 0.5f),
	                                         new Vector3(SIZE, SIZE, SIZE));

	[field: SerializeField] public Transform PlayerTransform  { get; private set; }
	public                         int3      PlayerChunkCoord { get; private set; }

	public int       Seed     => seed;
	public FastNoise WorldGen { get; private set; }

	[NonSerialized] public NativeArray<BlockTypesJob> BlockTypesJobs;

	public bool TryGetChunkFromVector3(Vector3 pos, out Chunk chunk)
	{
		return chunkStorage.TryGetValue(WorldToChunkCoord(pos), out chunk);
	}

	public bool TryGetChunk(int3 coord, out Chunk chunk)
	{
		return chunkStorage.TryGetValue(coord, out chunk);
	}
	
	public void EditVoxelAt(Vector3 worldPos, ushort blockId)
	{
		int3 coord = WorldToChunkCoord(worldPos);
		if (!chunkStorage.TryGetValue(coord, out Chunk chunk)) return;
		chunk.EditVoxel(new int3(worldPos), blockId);
		chunkStorage[coord] = chunk;
	}
	
	public void RebuildChunkMesh(int3 coord)
	{
		if (!chunkStorage.TryGetValue(coord, out Chunk chunk)) return;
		chunk.ScheduleMeshDataJob();
		chunk.CreateMesh();
		chunkStorage[coord] = chunk;
	}

	#endregion

	#region Private state

	private int  chunkLayer;
	private int3 playerLastChunkCoord;
	private byte lastViewDistance = VoxelData.ViewDistanceInChunks;

	private Stack<Chunk> chunkPool;
	private int          chunkPoolMaxSize;
	private ColliderPool colliderGoPool;

	private readonly Dictionary<int3, Chunk>      chunkStorage    = new();
	private readonly Dictionary<int3, GameObject> activeColliders = new();

	private readonly SimpleFastPriorityQueue<int3, float> chunksToCreate =
		new(new Int3EqualityComparer());

	private readonly Queue<Chunk>  pendingBakes            = new();
	private readonly List<int3>    chunksToRelease         = [];
	private readonly List<int3>    chunksToRemoveFromQueue = [];
	private readonly List<int3>    collidersToReturn       = [];
	private readonly HashSet<int3> desiredCoords           = [];
	private readonly HashSet<int3> renderCoords            = [];

	private RenderParams renderParams;

	#endregion

	#region Unity lifecycle

	private void Awake()
	{
		BlockTypesJobs = new NativeArray<BlockTypesJob>(blockTypes.Length, Allocator.Persistent);
		for (var i = 0; i < blockTypes.Length; i++)
			BlockTypesJobs[i] = blockTypes[i].BlockTypeData;

		BiomeAttributesJob = biomeAttributes.BiomeData;
		chunkLayer         = LayerMask.NameToLayer("Chunk");

		InitChunkPool();
		InitColliderPool();
		SetupRenderParams();
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
	}

	private void LateUpdate()
	{
		DrawChunks();
	}

	private void OnApplicationQuit()
	{
		foreach (Chunk c in chunkStorage.Select(pair => pair.Value))
		{
			c.OnDestroy();
		}

		chunkStorage.Clear();

		while (chunkPool.TryPop(out Chunk pooled))
			pooled.OnDestroy();

		activeColliders.Clear();
		colliderGoPool.Dispose();

		BlockTypesJobs.Dispose();
	}

	#endregion

	#region Rendering

	private void SetupRenderParams()
	{
		renderParams = new RenderParams(material)
		               {
			               layer                = chunkLayer,
			               renderingLayerMask   = RenderingLayerMask.defaultRenderingLayerMask,
			               rendererPriority     = 0,
			               worldBounds          = ChunkBound,
			               motionVectorMode     = MotionVectorGenerationMode.Camera,
			               reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox,
			               shadowCastingMode    = ShadowCastingMode.On,
			               receiveShadows       = true,
			               lightProbeUsage      = LightProbeUsage.BlendProbes,
			               matProps             = new MaterialPropertyBlock()
		               };
	}

	private void DrawChunks()
	{
		foreach (Chunk chunk in chunkStorage.Select(pair => pair.Value)
		                                    .Where(c => c is { IsActive: true, HasMesh: true }))
		{
			if (chunk.VerticesBuffer == null ||
			    chunk.IndicesBuffer == null ||
			    chunk.IndirectArgsBuffer == null) continue;

			int3 chunkPos = chunk.ChunkPosition;
			renderParams.worldBounds = new Bounds(chunkPos.ToVector3() + half, extents);

			chunk.MatProps.SetBuffer(verticesPropertyId, chunk.VerticesBuffer);
			chunk.MatProps.SetVector(chunkPositionPropertyId,
			                         new Vector4(chunkPos.x, chunkPos.y, chunkPos.z, 0f));
			renderParams.matProps = chunk.MatProps;

			Graphics.RenderPrimitivesIndexedIndirect(
			                                         in renderParams,
			                                         MeshTopology.Triangles,
			                                         chunk.IndicesBuffer,
			                                         chunk.IndirectArgsBuffer);
		}
	}

	#endregion

	#region Chunk queue

	private void ProcessChunkQueue()
	{
		if (chunksToCreate.Count == 0) return;

		var initsThisFrame = 0;
		chunksToRemoveFromQueue.Clear();
		
		foreach (int3 coord in chunksToCreate)
		{
			if (!chunkStorage.TryGetValue(coord, out Chunk chunk))
			{
				chunksToRemoveFromQueue.Add(coord);
				continue;
			}

			// Phase 1 — schedule the voxel populate job.
			if (!chunk.IsScheduled)
			{
				var isOuterRing = !renderCoords.Contains(coord);
				if (!isOuterRing && initsThisFrame >= maxChunkInitsPerFrame) continue;
				chunk.Initialise();
				chunkStorage[coord] = chunk;
				if (!isOuterRing) initsThisFrame++;
				continue;
			}

			// Phase 2 — wait for voxel job to finish.
			if (!chunk.VoxelMapPopulated) continue;

			// Phase 3 — outer-ring done, no mesh needed.
			if (!renderCoords.Contains(coord))
			{
				chunksToRemoveFromQueue.Add(coord);
				continue;
			}

			// Phase 4 — wait until every in-storage neighbor has its voxel map populated.
			if (!AllStoredNeighborsMapped(coord)) continue;

			// Phase 5 — schedule mesh job once.
			if (!chunk.IsMeshScheduled)
			{
				chunk.ScheduleMeshDataJob();
				chunkStorage[coord] = chunk;
				continue;
			}

			// Phase 6 — mesh job finished, upload to GPU.
			if (!chunk.IsMeshDataCompleted) continue;

			chunk.CreateMesh();
			chunkStorage[coord] = chunk;
			chunksToCreate.Remove(coord);
		}
		
		foreach (int3 coord in chunksToRemoveFromQueue.Where(coord => chunksToCreate.Contains(coord)))
			chunksToCreate.Remove(coord);
	}

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

	#region View distance

	private void CheckViewDistance()
	{
		int3 center       = WorldToChunkCoord(PlayerTransform.position);
		int  renderDist   = VoxelData.ViewDistanceInChunks;
		var  populateDist = renderDist + 1;

		desiredCoords.Clear();
		renderCoords.Clear();
		for (var y = center.y - populateDist; y < center.y + populateDist; y++)
		for (var x = center.x - populateDist; x < center.x + populateDist; x++)
		for (var z = center.z - populateDist; z < center.z + populateDist; z++)
		{
			int3 c = new(x, y, z);
			desiredCoords.Add(c);
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
			if (chunksToCreate.Contains(c)) chunksToCreate.Remove(c);
			ReturnCollider(c);
			if (chunkStorage.Remove(c, out Chunk chunk))
				ReturnChunkToPool(chunk);
		}

		foreach (int3 c in desiredCoords)
		{
			var dist = math.distance(center, c);
			if (!chunkStorage.ContainsKey(c))
				CreateNewChunk(c, dist);
			else if (chunksToCreate.Contains(c))
				chunksToCreate.UpdatePriority(c, dist);
			else if (renderCoords.Contains(c)
			         && chunkStorage.TryGetValue(c, out Chunk existing)
			         && existing is { VoxelMapPopulated: true, HasMesh: false, IsMeshScheduled: false })
				chunksToCreate.Enqueue(c, dist);
		}
	}

	public void OnChunkMeshReady(Chunk chunk)
	{
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

		int3       chunkPos = chunk.ChunkPosition;
		GameObject go       = colliderGoPool.Get();
		go.name                                    = $"ChunkCollider: {chunk.Coord}";
		go.transform.position                      = chunkPos.ToVector3();
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

	#region Pool

	private void InitChunkPool()
	{
		var viewDiam = (VoxelData.ViewDistanceInChunks + 1) * 2;
		chunkPoolMaxSize = viewDiam * viewDiam * viewDiam;
		chunkPool        = new Stack<Chunk>(chunkPoolMaxSize);
	}

	private Chunk GetChunkFromPool()
	{
		return chunkPool.TryPop(out Chunk chunk) ? chunk : default;
	}

	private void ReturnChunkToPool(Chunk chunk)
	{
		chunk.Release();
		if (chunkPool.Count < chunkPoolMaxSize)
			chunkPool.Push(chunk);
		else
			chunk.OnDestroy();
	}

	#endregion

	#region Helpers

	private void SetFastNoise()
	{
		WorldGen = FastNoise.FromEncodedNodeTree(encodedNodeTree);
	}

	private void CreateNewChunk(int3 coord, float priority)
	{
		Chunk chunk = GetChunkFromPool();
		chunk.Init(coord, this);
		chunkStorage[coord] = chunk;
		chunksToCreate.Enqueue(coord, priority);
	}

	private static int3 WorldToChunkCoord(Vector3 pos)
	{
		return new int3(
		                Mathf.FloorToInt(pos.x / VoxelData.CHUNK_SIZE),
		                Mathf.FloorToInt(pos.y / VoxelData.CHUNK_SIZE),
		                Mathf.FloorToInt(pos.z / VoxelData.CHUNK_SIZE));
	}

	#endregion
}

public sealed class Int3EqualityComparer : IEqualityComparer<int3>
{
	public bool Equals(int3 x, int3 y)
	{
		return x.x == y.x && x.y == y.y && x.z == y.z;
	}

	public int GetHashCode(int3 obj)
	{
		return HashCode.Combine(obj.x, obj.y, obj.z);
	}
}