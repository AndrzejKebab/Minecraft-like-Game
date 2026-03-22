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
public struct Chunk
{
	#region Public state

	public Mesh Mesh            { get; private set; }
	public bool HasMesh         { get; private set; }
	public int3 Coord           { get; private set; }
	public int3 ChunkPosition   { get; private set; }
	public bool IsActive        { get; private set; }
	public bool IsScheduled     { get; private set; }
	public bool IsMeshScheduled { get; private set; }
	public bool VoxelMapPopulated;
	public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;

	public GraphicsBuffer        VerticesBuffer     { get; private set; }
	public GraphicsBuffer        IndicesBuffer      { get; private set; }
	public GraphicsBuffer        IndirectArgsBuffer { get; private set; }
	public MaterialPropertyBlock MatProps           { get; private set; }

	#endregion

	#region Private state
	
	private NativeArray<ushort> voxelMap;

	private JobHandle      chunkJobHandle;
	private JobHandle      populateVoxelMapHandle;
	private NativeMeshData nativeMeshData;
	private VoxelMapData   voxelMapData;

	private List<NativeArray<ushort>> dummiesList;
	private NativeArray<ushort>[]     neighborDummies;

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

		Mesh     = new Mesh();
		MatProps = new MaterialPropertyBlock();
		
		if (!voxelMap.IsCreated)
			voxelMap = new NativeArray<ushort>(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE,
			                                   Allocator.Persistent);

		dummiesList     ??= new List<NativeArray<ushort>>(6);
		neighborDummies =   [];
	}

	public void Release()
	{
		chunkJobHandle.Complete();
		populateVoxelMapHandle.Complete();
		DisposeHeightMap();
		DisposeDummies();
		DisposeNativeMeshData();
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
		DisposeHeightMap();
		DisposeDummies();
		DisposeNativeMeshData();
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
		chunkJobHandle.Complete();
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
		DisposeNativeMeshData();

		IsMeshScheduled = true;

		nativeMeshData = new NativeMeshData
		           {
			           Vertex        = new NativeList<Vertex>(Allocator.Persistent),
			           MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		           };

		NativeChunkData chunkData = BuildChunkData(out JobHandle dependency);

		var chunkJob = new ChunkJob
		               {
			               MeshData  = nativeMeshData,
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
		
		if (!nativeMeshData.Vertex.IsCreated)
			return;

		if (nativeMeshData.Vertex.Length == 0)
		{
			ReleaseGfxBuffers();
			Mesh.Clear();
			HasMesh = false;
			DisposeNativeMeshData();
			return;
		}

		var vCount = nativeMeshData.Vertex.Length;
		var iCount = nativeMeshData.MeshTriangles.Length;
		
		var newVerts   = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vCount, sizeof(uint));
		var newIndices = new GraphicsBuffer(GraphicsBuffer.Target.Index, iCount, sizeof(ushort));
		var newIndirect = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
		                                     GraphicsBuffer.IndirectDrawIndexedArgs.size);

		newVerts.SetData(nativeMeshData.Vertex.AsArray());
		newIndices.SetData(nativeMeshData.MeshTriangles.AsArray());

		var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
		args[0].indexCountPerInstance = (uint)iCount;
		args[0].instanceCount         = 1;
		newIndirect.SetData(args);
		
		ReleaseGfxBuffers();
		VerticesBuffer     = newVerts;
		IndicesBuffer      = newIndices;
		IndirectArgsBuffer = newIndirect;

		Mesh.Clear();
		Mesh.MeshDataArray meshData = Mesh.AllocateWritableMeshData(1);
		BuildColliderMesh(vCount, iCount, ref nativeMeshData, ref meshData);
		
		Mesh.name      = $"Chunk [{Coord.x},{Coord.y},{Coord.z}]";
		Mesh.ApplyAndDisposeWritableMeshData(meshData, Mesh);
		DisposeNativeMeshData();
		HasMesh = true;
		world.OnChunkMeshReady(this);
	}

	#endregion

	#region Voxel editing

	public void EditVoxel(int3 pos, ushort blockId)
	{
		int3 local = pos - ChunkPosition;
		voxelMap.SetAtIndex(local.x, local.y, local.z, blockId);
		
		ScheduleMeshDataJob();
		CreateMesh();
		UpdateNeighborMeshes(local);
	}

	private void UpdateNeighborMeshes(int3 localPos)
	{
		for (var face = 0; face < 6; face++)
		{
			int3 neighborLocal = localPos + FaceChecks[face];
			if (IsLocalPosInChunk(ref neighborLocal)) continue;

			world.RebuildChunkMesh(Coord + FaceChecks[face]);
		}
	}

	#endregion

	#region Private helpers

	[BurstCompile]
	private static void BuildColliderMesh(int vCount, int iCount, ref NativeMeshData meshData, ref Mesh.MeshDataArray mesh)
	{
		Mesh.MeshData data = mesh[0];
		data.subMeshCount = 1;
		var descriptors = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Temp)
		{
			[0] = new VertexAttributeDescriptor(
				      VertexAttribute.Position)
		};
		data.SetVertexBufferParams(vCount, descriptors);
		data.SetIndexBufferParams(iCount, IndexFormat.UInt16);

		NativeArray<Vector3> positions = data.GetVertexData<Vector3>();
		NativeArray<ushort>  indices   = data.GetIndexData<ushort>();

		for (var i = 0; i < vCount; i++)
		{
			var d     = meshData.Vertex[i].Data;
			positions[i] = new Vector3(d & 0x3Fu, (d >> 6) & 0x3Fu, (d >> 12) & 0x3Fu);
		}

		for (var i = 0; i < iCount; i++)
			indices[i] = meshData.MeshTriangles[i];

		data.SetSubMesh(0, new SubMeshDescriptor(0, iCount), MeshUpdateFlags.DontRecalculateBounds);
		descriptors.Dispose();
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
		if (neighborDummies == null) return;
		foreach (NativeArray<ushort> d in neighborDummies)
			if (d.IsCreated)
				d.Dispose();
		neighborDummies = [];
	}

	private void DisposeNativeMeshData()
	{
		if (nativeMeshData.Vertex.IsCreated) nativeMeshData.Vertex.Dispose();
		if (nativeMeshData.MeshTriangles.IsCreated) nativeMeshData.MeshTriangles.Dispose();
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