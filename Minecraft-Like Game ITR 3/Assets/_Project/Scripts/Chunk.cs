using System.Collections.Generic;
using FastNoise2.Bindings;
using NativeTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static ChunkJob;
using static PopulateVoxelMapJob;
using static VoxelData;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class Chunk
{
	#region Public state

	// Mesh is kept solely for the MeshCollider — it is NOT used for rendering.
	// Rendering goes through VerticesBuffer / IndicesBuffer / IndirectArgsBuffer.
	public Mesh Mesh              { get; } = new();
	public bool HasMesh           { get; private set; }
	public int3 Coord             { get; private set; }
	public int3 ChunkPosition     { get; private set; }
	public bool IsActive          { get; private set; } = true;
	public bool IsScheduled       { get; private set; }
	public bool IsMeshScheduled   { get; private set; }
	public bool VoxelMapPopulated;
	public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;

	// GPU buffers consumed by World.DrawChunks via RenderPrimitivesIndexedIndirect
	public GraphicsBuffer VerticesBuffer     { get; private set; }
	public GraphicsBuffer IndicesBuffer      { get; private set; }
	public GraphicsBuffer IndirectArgsBuffer { get; private set; }
	
	// Per-chunk property block — World sets Vertices on it each draw call
	public readonly MaterialPropertyBlock MatProps = new();

	#endregion

	#region Private native data

	private NativeArray<ushort> voxelMap =
		new(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE, Allocator.Persistent);

	private NativeArray<ushort> VoxelMap              => voxelMap;
	private JobHandle           PopulateVoxelMapHandle => populateVoxelMapHandle;

	#endregion

	#region Private job state

	private JobHandle      chunkJobHandle;
	private JobHandle      populateVoxelMapHandle;
	private NativeMeshData meshData;
	private VoxelMapData   voxelMapData;

	private readonly List<NativeArray<ushort>> dummiesList     = new(6);
	private          NativeArray<ushort>[]     neighborDummies = [];

	private NativeTexture2D<float> heightMap;
	private World                  world;

	#endregion

	#region Pool lifecycle

	public void Init(int3 coord, World worldRef)
	{
		Coord             = coord;
		world             = worldRef;
		ChunkPosition     = coord * CHUNK_SIZE;
		IsActive          = true;
		IsScheduled       = false;
		IsMeshScheduled   = false;
		VoxelMapPopulated = false;
		HasMesh           = false;
	}

	public void Release()
	{
		chunkJobHandle.Complete();
		populateVoxelMapHandle.Complete();
		DisposeHeightMap();
		DisposeDummies();
		DisposeMeshData();
		ReleaseGfxBuffers();

		Mesh.Clear();
		HasMesh           = false;
		IsScheduled       = false;
		IsMeshScheduled   = false;
		VoxelMapPopulated = false;
		IsActive          = false;
	}

	public void OnDestroy()
	{
		chunkJobHandle.Complete();
		populateVoxelMapHandle.Complete();
		DisposeDummies();
		DisposeMeshData();
		ReleaseGfxBuffers();

		if (voxelMap.IsCreated) voxelMap.Dispose();
	}

	#endregion

	#region Chunk lifecycle

	public void Initialise()
	{
		IsScheduled = true;
		SchedulePopulateVoxelMap();
	}

	private void SchedulePopulateVoxelMap()
	{
		voxelMapData = new VoxelMapData
		{
			ChunkSize         = CHUNK_SIZE,
			WorldSizeInVoxels = WorldSizeInVoxels,
			BiomeData         = world.BiomeAttributesJob
		};

		FastNoise worldGen      = world.WorldGen;
		int3      chunkWorldPos = ChunkPosition;
		NoiseGenerator.GenerateHeightMap(out heightMap,
		                                 ref worldGen,
		                                 ref chunkWorldPos,
		                                 CHUNK_SIZE,
		                                 world.BiomeAttributesJob.BiomeScale,
		                                 world.Seed);

		VoxelMapPopulated = true;
		var populateJob = new PopulateVoxelMapJob
		{
			HeightMap     = heightMap.AsReadOnly(),
			VoxelData     = voxelMapData,
			ChunkPosition = chunkWorldPos,
			VoxelMap      = voxelMap
		};
		populateVoxelMapHandle = populateJob.ScheduleByRef();
	}

	public void ScheduleMeshDataJob()
	{
		// Complete and discard any in-flight job before overwriting its NativeLists.
		chunkJobHandle.Complete();
		DisposeMeshData();

		IsMeshScheduled = true;

		meshData = new NativeMeshData
		{
			Vertex        = new NativeList<Vertex>(Allocator.Persistent),
			MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		};

		// No MeshDataArray — we upload to GraphicsBuffers ourselves after the job.
		NativeChunkData chunkData = BuildChunkData(out JobHandle dependency);

		var chunkJob = new ChunkJob
		{
			MeshData          = meshData,
			ChunkData         = chunkData,
			ChunkSize         = CHUNK_SIZE,
			Position          = ChunkPosition,
			WorldSizeInVoxels = WorldSizeInVoxels,
			// TextureAtlasSize / NormalizedTextureAtlas removed — not needed for Texture2DArray
		};
		chunkJobHandle = chunkJob.ScheduleByRef(dependency);
	}

	public void CreateMesh()
	{
		chunkJobHandle.Complete();
		DisposeHeightMap();
		DisposeDummies();

		// Guard: meshData is disposed after a successful build. If CreateMesh is
		// called a second time in the same frame (e.g. ProcessChunkQueue + EditVoxel
		// both reaching Phase 6 for the same chunk), return early and keep the
		// existing GPU buffers — no flicker, no stale draw.
		if (!meshData.Vertex.IsCreated)
			return;

		if (meshData.Vertex.Length == 0)
		{
			ReleaseGfxBuffers();
			Mesh.Clear();
			HasMesh = false;
			DisposeMeshData();
			return;
		}

		int vCount = meshData.Vertex.Length;
		int iCount = meshData.MeshTriangles.Length;

		// Allocate brand-new buffers sized exactly to this mesh.
		// In-place SetData into a pre-allocated buffer can cause a one-frame
		// white flash on block removal: the GPU may still be reading the old
		// buffer contents when SetData overwrites them, because Unity must
		// internally fence the pipeline — and that fence isn't always invisible.
		// A freshly allocated buffer has never been referenced by a draw command,
		// so there is no synchronization hazard.
		var newVerts    = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vCount, sizeof(uint));
		var newIndices  = new GraphicsBuffer(GraphicsBuffer.Target.Index,      iCount, sizeof(ushort));
		var newIndirect = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
		                                      GraphicsBuffer.IndirectDrawIndexedArgs.size);

		newVerts.SetData(meshData.Vertex.AsArray());
		newIndices.SetData(meshData.MeshTriangles.AsArray());

		var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
		args[0].indexCountPerInstance = (uint)iCount;
		args[0].instanceCount         = 1;
		args[0].startIndex            = 0;
		args[0].baseVertexIndex       = 0;
		args[0].startInstance         = 0;
		newIndirect.SetData(args);

		// Atomic swap: release old buffers and assign new ones in one block.
		// Old buffers stay alive until this line — DrawChunks in LateUpdate
		// cannot observe a frame where VerticesBuffer is null.
		ReleaseGfxBuffers();
		VerticesBuffer     = newVerts;
		IndicesBuffer      = newIndices;
		IndirectArgsBuffer = newIndirect;

		Mesh.Clear();
		BuildColliderMesh(vCount, iCount);

		DisposeMeshData();
		HasMesh = true;
		world.OnChunkMeshReady(this);
	}

	#endregion

	#region Voxel editing

	public void EditVoxel(int3 pos, ushort blockId)
	{
		int3 local = pos - ChunkPosition;
		voxelMap[WorldExtensions.FlattenIndex(local.x, local.y, local.z)] = blockId;

		UpdateNeighborMeshes(local);
		ScheduleMeshDataJob();
		CreateMesh();
	}

	private void UpdateNeighborMeshes(int3 localPos)
	{
		for (var face = 0; face < 6; face++)
		{
			int3 neighborLocal = localPos + FaceChecks[face];
			if (IsLocalPosInChunk(ref neighborLocal)) continue;

			Chunk neighbor = world.GetChunkFromVector3(math.float3(neighborLocal + ChunkPosition));
			neighbor.ScheduleMeshDataJob();
			neighbor.CreateMesh();
		}
	}

	#endregion

	#region Private helpers

	/// <summary>
	/// Builds a position-only CPU Mesh used by MeshCollider.
	/// Unpacks integer XYZ from the packed uint vertex data.
	/// </summary>
	private void BuildColliderMesh(int vCount, int iCount)
	{
		var positions = new Vector3[vCount];
		for (var i = 0; i < vCount; i++)
		{
			uint d        = meshData.Vertex[i].Data;
			positions[i]  = new Vector3(d & 0x3Fu, (d >> 6) & 0x3Fu, (d >> 12) & 0x3Fu);
		}

		var triangles = new int[iCount];
		for (var i = 0; i < iCount; i++)
			triangles[i] = meshData.MeshTriangles[i];

		Mesh.name      = $"Chunk [{Coord.x},{Coord.y},{Coord.z}]";
		Mesh.vertices  = positions;
		Mesh.triangles = triangles;
		Mesh.bounds    = world.ChunkBound;
	}

	private NativeChunkData BuildChunkData(out JobHandle combinedDependency)
	{
		DisposeDummies();
		dummiesList.Clear();

		var data = new NativeChunkData
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
			int3 neighborCoord = Coord + FaceChecks[face];
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
				dummiesList.Add(map);
			}

			SetNeighborData(ref data, face, ref map, hasNeighbor);
		}

		combinedDependency = JobHandle.CombineDependencies(deps.GetSubArray(0, depCount));
		deps.Dispose();

		neighborDummies = dummiesList.ToArray();
		return data;
	}

	[BurstCompile]
	private static void SetNeighborData(ref NativeChunkData data, int face,
	                                    ref NativeArray<ushort> map, bool present)
	{
		switch (face)
		{
			case 0: data.NeighborZNeg = map; data.HasNeighborZNeg = present; break;
			case 1: data.NeighborZPos = map; data.HasNeighborZPos = present; break;
			case 2: data.NeighborYPos = map; data.HasNeighborYPos = present; break;
			case 3: data.NeighborYNeg = map; data.HasNeighborYNeg = present; break;
			case 4: data.NeighborXNeg = map; data.HasNeighborXNeg = present; break;
			case 5: data.NeighborXPos = map; data.HasNeighborXPos = present; break;
		}
	}

	/// <summary>Releases all three GPU GraphicsBuffers if they exist.</summary>
	private void ReleaseGfxBuffers()
	{
		VerticesBuffer?.Release();
		IndicesBuffer?.Release();
		IndirectArgsBuffer?.Release();
	}

	private void DisposeHeightMap()
	{
		if (heightMap.IsCreated) heightMap.Dispose();
	}

	private void DisposeDummies()
	{
		foreach (NativeArray<ushort> d in neighborDummies)
			if (d.IsCreated) d.Dispose();
		neighborDummies = [];
	}

	private void DisposeMeshData()
	{
		if (meshData.Vertex.IsCreated) meshData.Vertex.Dispose();
		if (meshData.MeshTriangles.IsCreated) meshData.MeshTriangles.Dispose();
	}

	[BurstCompile]
	private static bool IsLocalPosInChunk(ref int3 pos)
	{
		return pos.x is >= 0 and < CHUNK_SIZE &&
		       pos.y is >= 0 and < CHUNK_SIZE &&
		       pos.z is >= 0 and < CHUNK_SIZE;
	}

	#endregion
}