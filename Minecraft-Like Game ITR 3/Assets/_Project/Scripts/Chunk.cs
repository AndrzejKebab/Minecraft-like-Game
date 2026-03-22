using System.Collections.Generic;
using FastNoise2.Bindings;
using NativeTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static ChunkJob;
using static PopulateVoxelMapJob;
using static VoxelData;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class Chunk
{
	#region Public state

	public Mesh Mesh            { get; } = new();
	public bool HasMesh         { get; private set; }
	public int3 Coord           { get; private set; }
	public int3 ChunkPosition   { get; private set; }
	public bool IsActive        { get; private set; } = true;
	public bool IsScheduled     { get; private set; }
	public bool IsMeshScheduled { get; private set; }
	public bool VoxelMapPopulated;
	public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;

	public GraphicsBuffer VerticesBuffer     { get; private set; }
	public GraphicsBuffer IndicesBuffer      { get; private set; }
	public GraphicsBuffer IndirectArgsBuffer { get; private set; }

	public readonly MaterialPropertyBlock MatProps = new();

	#endregion
	
	#region Private job state
	
	private NativeArray<ushort> voxelMap =
		new(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE, Allocator.Persistent);
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
			               ChunkSize = CHUNK_SIZE,
			               BiomeData = world.BiomeAttributesJob
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
		chunkJobHandle.Complete();
		DisposeMeshData();

		IsMeshScheduled = true;

		meshData = new NativeMeshData
		           {
			           Vertex        = new NativeList<Vertex>(Allocator.Persistent),
			           MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		           };

		NativeChunkData chunkData = BuildChunkData(out JobHandle dependency);

		var chunkJob = new ChunkJob
		               {
			               MeshData  = meshData,
			               ChunkData = chunkData,
			               ChunkSize = CHUNK_SIZE,
			               Position  = ChunkPosition
		               };
		chunkJobHandle = chunkJob.ScheduleByRef(dependency);
	}

	[BurstDiscard]
	public void CreateMesh()
	{
		chunkJobHandle.Complete();
		DisposeHeightMap();
		DisposeDummies();
		
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

		var vCount = meshData.Vertex.Length;
		var iCount = meshData.MeshTriangles.Length;
		
		var newVerts   = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vCount, sizeof(uint));
		var newIndices = new GraphicsBuffer(GraphicsBuffer.Target.Index, iCount, sizeof(ushort));
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
		voxelMap.SetAtIndex(local.x, local.y, local.z, blockId);
		
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

			world.TryGetChunkFromVector3(math.float3(neighborLocal + ChunkPosition), out Chunk neighbor);
			neighbor.ScheduleMeshDataJob();
			neighbor.CreateMesh();
		}
	}

	#endregion

	#region Private helpers
	
	private void BuildColliderMesh(int vCount, int iCount)
	{
		var positions = new Vector3[vCount];
		for (var i = 0; i < vCount; i++)
		{
			var d = meshData.Vertex[i].Data;
			positions[i] = new Vector3(d & 0x3Fu, (d >> 6) & 0x3Fu, (d >> 12) & 0x3Fu);
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
			           BlockTypes = world.BlockTypesJobs
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
				map              = neighbor.voxelMap;
				deps[depCount++] = neighbor.populateVoxelMapHandle;
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
	private static void SetNeighborData(ref NativeChunkData     data, int  face,
	                                    ref NativeArray<ushort> map,  bool present)
	{
		switch (face)
		{
			case 0:
				data.NeighborZNeg    = map;
				data.HasNeighborZNeg = present;
				break;
			case 1:
				data.NeighborZPos    = map;
				data.HasNeighborZPos = present;
				break;
			case 2:
				data.NeighborYPos    = map;
				data.HasNeighborYPos = present;
				break;
			case 3:
				data.NeighborYNeg    = map;
				data.HasNeighborYNeg = present;
				break;
			case 4:
				data.NeighborXNeg    = map;
				data.HasNeighborXNeg = present;
				break;
			case 5:
				data.NeighborXPos    = map;
				data.HasNeighborXPos = present;
				break;
		}
	}

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
			if (d.IsCreated)
				d.Dispose();
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