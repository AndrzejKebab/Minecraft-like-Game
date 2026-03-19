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

	private readonly Mesh mesh = new();

	private JobHandle               chunkJobHandle;
	private GameObject              chunkObject;
	private bool                    isActive   = true;
	public  bool                    IsUpdating = true;
	private MeshCollider            meshCollider;
	private ChunkJob.NativeMeshData meshData;
	private Mesh.MeshDataArray      meshDataArray;
	private MeshFilter              meshFilter;
	private MeshRenderer            meshRenderer;

	private NativeArray<ushort>[] neighborDummies = [];
	private JobHandle             populateVoxelMapHandle;

	private NativeArray<ushort> voxelMap =
		new((int)Mathf.Pow(VoxelData.CHUNK_SIZE, 3), Allocator.Persistent);

	private VoxelMapData voxelMapData;

	public  bool  VoxelMapPopulated;
	private World world;

	private int3 Coord { get; set; }

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

	private float3 ChunkPosition { get; set; }

	private NativeArray<ushort> VoxelMap => voxelMap;

	private JobHandle PopulateVoxelMapHandle => populateVoxelMapHandle;

	public void Init(int3 coord, World worldRef)
	{
		Coord             = coord;
		world             = worldRef;
		isActive          = true;
		IsScheduled       = false;
		VoxelMapPopulated = false;
		IsUpdating        = true;
	}

	public void Release()
	{
		chunkJobHandle.Complete();
		populateVoxelMapHandle.Complete();

		DisposeDummies();

		if (meshData.Vertex.IsCreated) meshData.Vertex.Dispose();
		if (meshData.MeshTriangles.IsCreated) meshData.MeshTriangles.Dispose();

		if (chunkObject != null)
		{
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

	public void Initialise()
	{
		IsScheduled = true;

		if (chunkObject is null)
		{
			chunkObject  = new GameObject();
			meshFilter   = chunkObject.AddComponent<MeshFilter>();
			meshRenderer = chunkObject.AddComponent<MeshRenderer>();
			meshCollider = chunkObject.AddComponent<MeshCollider>();
		}

		chunkObject.SetActive(false);
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
			                         NodeHandle = world.WorldGenNodePtr
		                         }.Schedule();

		CreateMeshDataJob();
	}

	private void CreateMeshDataJob()
	{
		VoxelMapPopulated = true;
		IsUpdating        = true;

		meshData = new ChunkJob.NativeMeshData
		           {
			           Vertex        = new NativeList<Vertex>(Allocator.Persistent),
			           MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		           };

		meshDataArray = Mesh.AllocateWritableMeshData(1);

		ChunkJob.NativeChunkData chunkData = BuildChunkData(out JobHandle combinedDependency);

		chunkJobHandle = new ChunkJob
		                 {
			                 MeshData               = meshData,
			                 ChunkData              = chunkData,
			                 ChunkSize              = VoxelData.CHUNK_SIZE,
			                 TextureAtlasSize       = VoxelData.TEXTURE_ATLAS_SIZE_IN_BLOCKS,
			                 NormalizedTextureAtlas = VoxelData.NormalizedBlockTextureSize,
			                 Position               = new int3(ChunkPosition),
			                 WorldSizeInVoxels      = VoxelData.WorldSizeInVoxels,
			                 NodeHandle             = world.WorldGenNodePtr,
			                 MeshDataArray          = meshDataArray,
			                 Layout                 = layout
		                 }.Schedule(combinedDependency);
	}

	private ChunkJob.NativeChunkData BuildChunkData(out JobHandle combinedDependency)
	{
		DisposeDummies();

		var dummies = new List<NativeArray<ushort>>(6);

		var data = new ChunkJob.NativeChunkData
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
		neighborDummies = [];
	}

	public void CreateMesh()
	{
		chunkJobHandle.Complete();
		DisposeDummies();

		mesh.Clear();

		if (meshData.Vertex.Length == 0)
		{
			meshData.Vertex.Dispose();
			meshData.MeshTriangles.Dispose();
			meshDataArray.Dispose();
			IsUpdating = false;
			return;
		}

		mesh.name = "Chunk";

		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds);

		mesh.bounds = world.ChunkBound;
		mesh.RecalculateUVDistributionMetrics();

		meshFilter.sharedMesh = mesh;
		if (mesh.vertexCount > 0)
			meshCollider.sharedMesh = mesh;

		chunkObject.SetActive(true);

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

			if (IsVoxelInChunk(currentVoxel)) continue;
			Chunk neighbor = world.GetChunkFromVector3(math.float3(currentVoxel + ChunkPosition));
			neighbor.CreateMeshDataJob();
			neighbor.CreateMesh();
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