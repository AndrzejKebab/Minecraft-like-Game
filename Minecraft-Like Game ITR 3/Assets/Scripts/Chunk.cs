using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static PopulateVoxelMapJob;
using Object = UnityEngine.Object;

public class Chunk(int3 coord, World world)
{
	private GameObject chunkObject;
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;
	private MeshCollider meshCollider;
	private readonly Mesh mesh = new();

	private int3 Coord { get; } = coord;
	private bool isActive = true;

	public bool IsActive
	{
		get => isActive;
		set
		{
			isActive = value;
			chunkObject?.SetActive(value);
		}
	}

	public bool IsScheduled { get; private set; }
	public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;
	public bool IsVoxelMapCompleted => populateVoxelMapHandle.IsCompleted;

	public bool VoxelMapPopulated;
	public bool IsUpdating = true;

	private float3 ChunkPosition { get; set; }

	private NativeArray<ushort> voxelMap =
		new((int)Mathf.Pow(VoxelData.CHUNK_SIZE, 3), Allocator.Persistent);

	private JobHandle chunkJobHandle;
	private ChunkJob.MeshData meshData;
	private JobHandle populateVoxelMapHandle;
	private VoxelMapData voxelMapData;
	
	private readonly NativeArray<VertexAttributeDescriptor> layout = new (4, Allocator.Persistent)
	                                                {
		                                                [0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
		                                                [1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
		                                                [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.UNorm8, 4),
		                                                [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
	                                                };
	private Mesh.MeshDataArray meshDataArray;

	public void Initialise()
	{
		IsScheduled = true;
		chunkObject = new GameObject();

		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();
		meshCollider = chunkObject.AddComponent<MeshCollider>();
		meshRenderer.material = world.Material;

		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(Coord.x * VoxelData.CHUNK_SIZE, Coord.y * VoxelData.CHUNK_SIZE,
			Coord.z * VoxelData.CHUNK_SIZE);
		chunkObject.name = $"Chunk [{Coord.x}, {Coord.y}, {Coord.z}]";
		chunkObject.layer = LayerMask.NameToLayer("Chunk");

		ChunkPosition = chunkObject.transform.position;

		PopulateVoxelMap();
	}

	private void PopulateVoxelMap()
	{
		voxelMapData = new VoxelMapData
		{
			ChunkSize = VoxelData.CHUNK_SIZE,
			WorldSizeInVoxels = VoxelData.WorldSizeInVoxels,
			BiomeData = world.BiomeAttributesJob
		};

		populateVoxelMapHandle = new PopulateVoxelMapJob
		{
			Position = ChunkPosition,
			VoxelData = voxelMapData,
			VoxelMap = voxelMap,
			nodeHandle = world.WorldGenNodePtr
		}.Schedule();
		CreateMeshDataJob();
	}

	private void CreateMeshDataJob()
	{
		//populateVoxelMapHandle.Complete();
		VoxelMapPopulated = true;

		IsUpdating = true;
		meshData = new ChunkJob.MeshData
		{
			Vertex = new NativeList<Vertex>(Allocator.Persistent),
			MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
		};

		meshDataArray = Mesh.AllocateWritableMeshData(1);
		
		chunkJobHandle = new ChunkJob
		{
			meshData = meshData,
			chunkData = new ChunkJob.ChunkData
			{
				VoxelMap = voxelMap,
				BlockTypes = world.BlockTypesJobs,
				BiomeData = world.BiomeAttributesJob
			},

			ChunkSize = VoxelData.CHUNK_SIZE,
			TextureAtlasSize = VoxelData.TEXTURE_ATLAS_SIZE_IN_BLOCKS,
			NormalizedTextureAtlas = VoxelData.NormalizedBlockTextureSize,
			Position = new int3(ChunkPosition),
			WorldSizeInVoxels = VoxelData.WorldSizeInVoxels,
			nodeHandle = world.WorldGenNodePtr,
			MeshDataArray = meshDataArray,
			Layout = layout
		}.Schedule(populateVoxelMapHandle);
	}

	public void CreateMesh()
	{
		chunkJobHandle.Complete();
		mesh.Clear();
		if(meshDataArray[0].vertexBufferCount == 0) return;
		mesh.name = "Chunk";
		mesh.MarkDynamic();
		mesh.bounds = world.ChunkBound;
		
		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
		
		mesh.RecalculateUVDistributionMetrics();

		meshFilter.sharedMesh = mesh;
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
				world.GetChunkFromVector3(math.float3(currentVoxel + ChunkPosition)).CreateMeshDataJob();
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

		if (voxelMap.IsCreated) voxelMap.Dispose();
		if (meshData.Vertex.IsCreated) meshData.Vertex.Dispose();
		if (meshData.MeshTriangles.IsCreated) meshData.MeshTriangles.Dispose();
		IsUpdating = false;

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