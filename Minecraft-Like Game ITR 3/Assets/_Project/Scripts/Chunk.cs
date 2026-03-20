using System.Collections.Generic;
using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static ChunkJob;
using static PopulateVoxelMapJob;
using static VoxelData;

public class Chunk
{
	#region Public state

	public Mesh Mesh          { get; } = new();
	public bool HasMesh       { get; private set; }
	public int3 Coord         { get; private set; }
	public int3 ChunkPosition { get; private set; }
	public bool IsActive      { get; private set; } = true;
	public bool IsScheduled      { get; private set; }
	public bool IsMeshScheduled  { get; private set; }
	public bool VoxelMapPopulated;
	public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;

	#endregion

	#region Private native data

	private NativeArray<ushort> voxelMap =
		new(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE, Allocator.Persistent);

	private NativeArray<ushort> VoxelMap               => voxelMap;
	private JobHandle           PopulateVoxelMapHandle => populateVoxelMapHandle;

	#endregion

	#region Private job state

	private JobHandle          chunkJobHandle;
	private JobHandle          populateVoxelMapHandle;
	private NativeMeshData     meshData;
	private Mesh.MeshDataArray meshDataArray;
	private VoxelMapData       voxelMapData;

	private readonly List<NativeArray<ushort>> dummiesList     = new(6);
	private          NativeArray<ushort>[]     neighborDummies = [];

	private NativeTexture2D<float> heightMap;

	private World world;

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

		var worldGen = world.WorldGen;
		heightMap = NoiseGenerator.GenerateHeightMap(
			ref worldGen,
			ChunkPosition,
			CHUNK_SIZE,
			world.BiomeAttributesJob.BiomeScale,
			world.Seed);

		VoxelMapPopulated = true;
		populateVoxelMapHandle = new PopulateVoxelMapJob
		{
			HeightMap     = heightMap.AsReadOnly(),
			VoxelData     = voxelMapData,
			ChunkPosition = ChunkPosition,
			VoxelMap      = voxelMap
		}.Schedule();
	}

	public void ScheduleMeshDataJob()
	{
		IsMeshScheduled   = true;

		meshData = new NativeMeshData
		           {
			           Vertex        = new NativeList<Vertex>(Allocator.Persistent),
			           MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		           };

		meshDataArray = Mesh.AllocateWritableMeshData(1);

		NativeChunkData chunkData = BuildChunkData(out JobHandle dependency);

		chunkJobHandle = new ChunkJob
		                 {
			                 MeshData               = meshData,
			                 ChunkData              = chunkData,
			                 ChunkSize              = CHUNK_SIZE,
			                 TextureAtlasSize       = TEXTURE_ATLAS_SIZE_IN_BLOCKS,
			                 NormalizedTextureAtlas = NormalizedBlockTextureSize,
			                 Position               = ChunkPosition,
			                 WorldSizeInVoxels      = WorldSizeInVoxels,
			                 MeshDataArray          = meshDataArray,
			                 Layout                 = World.Layout,
		                 }.Schedule(dependency);
	}

	public void CreateMesh()
	{
		chunkJobHandle.Complete();
		DisposeHeightMap();
		DisposeDummies();

		Mesh.Clear();
		HasMesh = false;

		if (meshData.Vertex.Length == 0)
		{
			DisposeMeshData();
			return;
		}

		Mesh.name = $"Chunk [{Coord.x},{Coord.y},{Coord.z}]";
		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, Mesh, MeshUpdateFlags.DontRecalculateBounds);
		Mesh.bounds = world.ChunkBound;
		Mesh.RecalculateUVDistributionMetrics();

		meshData.Vertex.Dispose();
		meshData.MeshTriangles.Dispose();

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
			if (IsLocalPosInChunk(neighborLocal)) continue;

			Chunk neighbor = world.GetChunkFromVector3(math.float3(neighborLocal + ChunkPosition));
			neighbor.ScheduleMeshDataJob();
			neighbor.CreateMesh();
		}
	}

	#endregion

	#region Private helpers

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

			SetNeighborData(ref data, face, map, hasNeighbor);
		}

		combinedDependency = JobHandle.CombineDependencies(deps.GetSubArray(0, depCount));
		deps.Dispose();

		neighborDummies = dummiesList.ToArray();
		return data;
	}

	private static void SetNeighborData(ref NativeChunkData data, int  face,
	                                    NativeArray<ushort> map,  bool present)
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

	private static bool IsLocalPosInChunk(int3 pos)
	{
		return pos.x is >= 0 and < CHUNK_SIZE &&
		       pos.y is >= 0 and < CHUNK_SIZE &&
		       pos.z is >= 0 and < CHUNK_SIZE;
	}

	#endregion
}