using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static PopulateVoxelMapJob;
using Object = UnityEngine.Object;

public class Chunk
{
	private          GameObject   chunkObject;
	private          MeshRenderer meshRenderer;
	private          MeshFilter   meshFilter;
	private          MeshCollider meshCollider;
	private readonly Mesh         mesh = new();

	private int3  Coord { get; set; }
	private World world;
	private bool  isActive = true;

	public bool IsActive
	{
		get => isActive;
		set
		{
			isActive = value;
			chunkObject?.SetActive(value);
		}
	}

	public bool IsScheduled         { get; private set; }
	public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;
	public bool IsVoxelMapCompleted => populateVoxelMapHandle.IsCompleted;

	public bool VoxelMapPopulated;
	public bool IsUpdating = true;

	private float3 ChunkPosition { get; set; }

	// Allocated once at construction and reused across pool cycles — no reallocation needed.
	private NativeArray<ushort> voxelMap =
		new((int)Mathf.Pow(VoxelData.CHUNK_SIZE, 3), Allocator.Persistent);

	/// <summary>
	///     Read-only view of this chunk's voxel map, used by neighbors when building
	///     their meshes so border faces correctly reflect player edits.
	///     Only valid after <see cref="VoxelMapPopulated" /> is true.
	/// </summary>
	public NativeArray<ushort> VoxelMap => voxelMap;

	/// <summary>
	///     The job handle for the populate step. Neighbors depend on this when
	///     scheduling their own ChunkJob so we don't race on the voxel map data.
	/// </summary>
	public JobHandle PopulateVoxelMapHandle => populateVoxelMapHandle;

	private JobHandle         chunkJobHandle;
	private ChunkJob.MeshData meshData;
	private JobHandle         populateVoxelMapHandle;
	private VoxelMapData      voxelMapData;

	// Zero-length dummy arrays for missing neighbor slots. Persistent so they
	// survive across multiple frames without triggering TempJob lifetime warnings.
	private NativeArray<ushort>[] neighborDummies = Array.Empty<NativeArray<ushort>>();

	// Persistent — vertex layout never changes, so allocate once per Chunk instance.
	private readonly NativeArray<VertexAttributeDescriptor> layout = new(4, Allocator.Persistent)
	                                                                 {
		                                                                 [0] =
			                                                                 new
				                                                                 VertexAttributeDescriptor(VertexAttribute
					                                                                  .Position,
				                                                                  VertexAttributeFormat.Float16, 4),
		                                                                 [1] =
			                                                                 new
				                                                                 VertexAttributeDescriptor(VertexAttribute
					                                                                  .Normal,
				                                                                  VertexAttributeFormat.SNorm8, 4),
		                                                                 [2] =
			                                                                 new
				                                                                 VertexAttributeDescriptor(VertexAttribute
					                                                                  .Tangent,
				                                                                  VertexAttributeFormat.UNorm8, 4),
		                                                                 [3] =
			                                                                 new
				                                                                 VertexAttributeDescriptor(VertexAttribute
					                                                                  .TexCoord0,
				                                                                  VertexAttributeFormat.Float16, 2)
	                                                                 };

	private Mesh.MeshDataArray meshDataArray;

	// -------------------------------------------------------------------------
	// Pooling lifecycle
	// -------------------------------------------------------------------------

	/// <summary>
	///     Parameterless constructor required by <see cref="UnityEngine.Pool.ObjectPool{T}" />.
	///     Call <see cref="Init" /> before using the chunk.
	/// </summary>
	public Chunk()
	{
	}

	/// <summary>
	///     Called by the pool's <c>actionOnGet</c> to (re)associate this instance with
	///     a world coordinate and world reference before <see cref="Initialise" /> runs.
	/// </summary>
	public void Init(int3 coord, World worldRef)
	{
		Coord             = coord;
		world             = worldRef;
		isActive          = true;
		IsScheduled       = false;
		VoxelMapPopulated = false;
		IsUpdating        = true;
	}

	/// <summary>
	///     Called by the pool's <c>actionOnRelease</c>. Completes any in-flight jobs,
	///     cleans up per-frame allocations, and hides the GameObject so it can be
	///     reassigned to a different coord on the next <see cref="Init" /> call.
	///     The persistent NativeArrays (<see cref="voxelMap" />, <see cref="layout" />)
	///     are deliberately kept alive for reuse.
	/// </summary>
	public void Release()
	{
		chunkJobHandle.Complete();
		populateVoxelMapHandle.Complete();

		DisposeDummies();

		if (meshData.Vertex.IsCreated) meshData.Vertex.Dispose();
		if (meshData.MeshTriangles.IsCreated) meshData.MeshTriangles.Dispose();

		if (chunkObject != null)
		{
			// Null the collider and filter references BEFORE clearing the mesh.
			// MeshCollider validates its mesh on SetActive — if we only called
			// mesh.Clear() the collider would still hold a reference to a now-empty
			// mesh and log a warning the next time this pooled chunk is reactivated.
			meshCollider.sharedMesh = null;
			meshFilter.sharedMesh   = null;
			mesh.Clear();
			chunkObject.SetActive(false);
		}

		IsScheduled       = false;
		VoxelMapPopulated = false;
		IsUpdating        = false;
		isActive          = false;
	}

	// -------------------------------------------------------------------------
	// Chunk lifecycle
	// -------------------------------------------------------------------------

	public void Initialise()
	{
		IsScheduled = true;

		if (chunkObject == null)
		{
			// First time this pooled instance is used — create the GameObject once.
			chunkObject           = new GameObject();
			meshFilter            = chunkObject.AddComponent<MeshFilter>();
			meshRenderer          = chunkObject.AddComponent<MeshRenderer>();
			meshCollider          = chunkObject.AddComponent<MeshCollider>();
			meshFilter.sharedMesh = mesh;
		}

		// Reuse the existing GameObject; just reposition and re-parent it.
		chunkObject.SetActive(true);
		meshRenderer.material = world.Material;
		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(
		                                             Coord.x * VoxelData.CHUNK_SIZE,
		                                             Coord.y * VoxelData.CHUNK_SIZE,
		                                             Coord.z * VoxelData.CHUNK_SIZE);
		chunkObject.name  = $"Chunk [{Coord.x}, {Coord.y}, {Coord.z}]";
		chunkObject.layer = LayerMask.NameToLayer("Chunk");

		ChunkPosition = chunkObject.transform.position;

		PopulateVoxelMap();
	}

	private void PopulateVoxelMap()
	{
		voxelMapData = new VoxelMapData
		               {
			               ChunkSize         = VoxelData.CHUNK_SIZE,
			               WorldSizeInVoxels = VoxelData.WorldSizeInVoxels,
			               BiomeData         = world.BiomeAttributesJob
		               };

		populateVoxelMapHandle = new PopulateVoxelMapJob
		                         {
			                         Position   = ChunkPosition,
			                         VoxelData  = voxelMapData,
			                         VoxelMap   = voxelMap,
			                         nodeHandle = world.WorldGenNodePtr
		                         }.Schedule();

		CreateMeshDataJob();
	}

	public void CreateMeshDataJob()
	{
		VoxelMapPopulated = true;
		IsUpdating        = true;

		meshData = new ChunkJob.MeshData
		           {
			           Vertex        = new NativeList<Vertex>(Allocator.Persistent),
			           MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		           };

		meshDataArray = Mesh.AllocateWritableMeshData(1);

		ChunkJob.ChunkData chunkData = BuildChunkData(out JobHandle combinedDependency);

		chunkJobHandle = new ChunkJob
		                 {
			                 meshData               = meshData,
			                 chunkData              = chunkData,
			                 ChunkSize              = VoxelData.CHUNK_SIZE,
			                 TextureAtlasSize       = VoxelData.TEXTURE_ATLAS_SIZE_IN_BLOCKS,
			                 NormalizedTextureAtlas = VoxelData.NormalizedBlockTextureSize,
			                 Position               = new int3(ChunkPosition),
			                 WorldSizeInVoxels      = VoxelData.WorldSizeInVoxels,
			                 nodeHandle             = world.WorldGenNodePtr,
			                 MeshDataArray          = meshDataArray,
			                 Layout                 = layout
		                 }.Schedule(combinedDependency);
	}

	private ChunkJob.ChunkData BuildChunkData(out JobHandle combinedDependency)
	{
		DisposeDummies();

		var dummies = new List<NativeArray<ushort>>(6);

		var data = new ChunkJob.ChunkData
		           {
			           VoxelMap   = voxelMap,
			           BlockTypes = world.BlockTypesJobs,
			           BiomeData  = world.BiomeAttributesJob
		           };

		var deps     = new NativeArray<JobHandle>(7, Allocator.Temp);
		var depCount = 0;
		deps[depCount++] = populateVoxelMapHandle;

		for (var face = 0; face < 6; face++)
		{
			int3 neighborCoord = Coord + VoxelData.FaceChecks[face];
			var hasNeighbor = world.TryGetChunk(neighborCoord, out Chunk neighbor)
			                  && neighbor.VoxelMapPopulated;

			NativeArray<ushort> map;
			if (hasNeighbor)
			{
				map              = neighbor.VoxelMap;
				deps[depCount++] = neighbor.PopulateVoxelMapHandle;
			}
			else
			{
				map = new NativeArray<ushort>(0, Allocator.Persistent);
				dummies.Add(map);
			}

			switch (face)
			{
				case 0:
					data.NeighborZNeg    = map;
					data.HasNeighborZNeg = hasNeighbor;
					break;
				case 1:
					data.NeighborZPos    = map;
					data.HasNeighborZPos = hasNeighbor;
					break;
				case 2:
					data.NeighborYPos    = map;
					data.HasNeighborYPos = hasNeighbor;
					break;
				case 3:
					data.NeighborYNeg    = map;
					data.HasNeighborYNeg = hasNeighbor;
					break;
				case 4:
					data.NeighborXNeg    = map;
					data.HasNeighborXNeg = hasNeighbor;
					break;
				case 5:
					data.NeighborXPos    = map;
					data.HasNeighborXPos = hasNeighbor;
					break;
			}
		}

		combinedDependency = JobHandle.CombineDependencies(deps.GetSubArray(0, depCount));
		deps.Dispose();

		neighborDummies = dummies.ToArray();
		return data;
	}

	private void DisposeDummies()
	{
		foreach (NativeArray<ushort> d in neighborDummies)
			if (d.IsCreated)
				d.Dispose();
		neighborDummies = Array.Empty<NativeArray<ushort>>();
	}

	public void CreateMesh()
	{
		chunkJobHandle.Complete();
		DisposeDummies();

		mesh.Clear();

		// vertexBufferCount is always 1 after SetVertexBufferParams — it counts
		// buffers, not vertices. Use the actual vertex list length instead so
		// air chunks (no solid voxels) exit before touching the collider.
		if (meshData.Vertex.Length == 0)
		{
			meshData.Vertex.Dispose();
			meshData.MeshTriangles.Dispose();
			// Dispose the unwritten MeshDataArray to avoid a leak.
			meshDataArray.Dispose();
			IsUpdating = false;
			return;
		}

		mesh.name = "Chunk";
		mesh.MarkDynamic();
		mesh.bounds = world.ChunkBound;

		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
		mesh.RecalculateUVDistributionMetrics();

		meshFilter.sharedMesh = mesh;
		// Only assign to the collider when the mesh actually has geometry.
		// MeshCollider logs an error if assigned an empty mesh.
		if (mesh.vertexCount > 0)
			meshCollider.sharedMesh = mesh;

		meshData.Vertex.Dispose();
		meshData.MeshTriangles.Dispose();
		IsUpdating = false;
	}

	public void EditVoxel(int3 pos, ushort blockId)
	{
		var xCheck = Mathf.FloorToInt(pos.x);
		var yCheck = Mathf.FloorToInt(pos.y);
		var zCheck = Mathf.FloorToInt(pos.z);

		Vector3 position = chunkObject.transform.position;
		xCheck -= Mathf.FloorToInt(position.x);
		yCheck -= Mathf.FloorToInt(position.y);
		zCheck -= Mathf.FloorToInt(position.z);

		voxelMap[WorldExtensions.FlattenIndex(xCheck, yCheck, zCheck)] = blockId;

		UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
		CreateMeshDataJob();
		CreateMesh();
	}

	private void UpdateSurroundingVoxels(int x, int y, int z)
	{
		var thisVoxel = new int3(x, y, z);

		for (var p = 0; p < 6; p++)
		{
			int3 currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

			if (!IsVoxelInChunk(currentVoxel))
			{
				Chunk neighbor = world.GetChunkFromVector3(math.float3(currentVoxel + ChunkPosition));
				neighbor.CreateMeshDataJob();
				neighbor.CreateMesh();
			}
		}
	}

	public ushort GetVoxelFromGlobalVector3(Vector3 pos)
	{
		var xCheck = Mathf.FloorToInt(pos.x);
		var yCheck = Mathf.FloorToInt(pos.y);
		var zCheck = Mathf.FloorToInt(pos.z);

		xCheck -= Mathf.FloorToInt(ChunkPosition.x);
		yCheck -= Mathf.FloorToInt(ChunkPosition.y);
		zCheck -= Mathf.FloorToInt(ChunkPosition.z);

		return voxelMap[WorldExtensions.FlattenIndex(xCheck, yCheck, zCheck)];
	}

	/// <summary>
	///     Full teardown — called by the pool's <c>actionOnDestroy</c> when the pool
	///     is cleared or the pool capacity is exceeded. Disposes all native memory
	///     and destroys the GameObject.
	/// </summary>
	public void OnDestroy()
	{
		chunkJobHandle.Complete();
		populateVoxelMapHandle.Complete();
		DisposeDummies();

		if (voxelMap.IsCreated) voxelMap.Dispose();
		if (layout.IsCreated) layout.Dispose();
		if (meshData.Vertex.IsCreated) meshData.Vertex.Dispose();
		if (meshData.MeshTriangles.IsCreated) meshData.MeshTriangles.Dispose();

		if (chunkObject == null) return;
		Object.Destroy(chunkObject.GetComponent<MeshCollider>().sharedMesh);
		Object.Destroy(chunkObject.GetComponent<MeshFilter>().sharedMesh);
		Object.Destroy(chunkObject.GetComponent<MeshRenderer>().material);
		Object.Destroy(chunkObject);
	}

	private static bool IsVoxelInChunk(int3 pos)
	{
		return pos.x is >= 0 and <= VoxelData.CHUNK_SIZE - 1 &&
		       pos.y is >= 0 and <= VoxelData.CHUNK_SIZE - 1 &&
		       pos.z is >= 0 and <= VoxelData.CHUNK_SIZE - 1;
	}
}

[StructLayout(LayoutKind.Sequential)]
public struct Vertex(half4 position, sbyte4 normal, sbyte4 color, half2 uv)
{
	public half4  Position = position;
	public sbyte4 Normal   = normal;
	public sbyte4 Color    = color;
	public half2  UVs      = uv;
}

#pragma warning disable 0659
[Serializable]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public struct sbyte4(sbyte x, sbyte y, sbyte z, sbyte w)
	: IEquatable<sbyte4>, IFormattable
{
	public sbyte x = x, y = y, z = z, w = w;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(sbyte4 rhs)
	{
		return x == rhs.x && y == rhs.y && z == rhs.z && w == rhs.w;
	}

	public override bool Equals(object o)
	{
		return o is sbyte4 converted && Equals(converted);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString()
	{
		return $"sbyte4({x}, {y}, {z}, {w})";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string ToString(string format, IFormatProvider formatProvider)
	{
		return
			$"sbyte4({x.ToString(format, formatProvider)}, {y.ToString(format, formatProvider)}, {z.ToString(format, formatProvider)}, {w.ToString(format, formatProvider)})";
	}
}