using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Chunk
{
	private GameObject chunkObject;
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;
	private MeshCollider meshCollider;

	public int2 Coord { get; private set; }

	private NativeArray<short> voxelMap = new NativeArray<short>(VoxelData.ChunkWidth * VoxelData.ChunkHeight * VoxelData.ChunkWidth, Allocator.TempJob);

	private JobHandle chunkJobHandle;
	private JobHandle populateVoxelMapHandle;
	private ChunkJob.MeshData meshData;
	private PopulateVoxelMapJob.VoxelMapData voxelMapData;

	private World world;

	private bool isActive;

	private bool isScheduled = false;
	public bool IsScheduled { get { return isScheduled; } }

	public Chunk(int2 _coord, World _world)
	{
		world = _world;
		Coord = _coord;
		isActive = true;
	}

	public void Initialise()
	{
		isScheduled = true;
		chunkObject = new GameObject();

		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();
		meshCollider = chunkObject.AddComponent<MeshCollider>();

		meshRenderer.material = world.Material;
		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(Coord.x * VoxelData.ChunkWidth, 0, Coord.y * VoxelData.ChunkWidth);
		chunkObject.name = $"Chunk [{Coord.x}, {Coord.y}]";
		chunkObject.layer = LayerMask.NameToLayer("Chunk");

		PopulateVoxelMapJob();
		CreateMeshDataJob();
	}

	private void PopulateVoxelMapJob()
	{
		voxelMapData = new PopulateVoxelMapJob.VoxelMapData
		{
			ChunkWidth = VoxelData.ChunkWidth,
			ChunkHeight = VoxelData.ChunkHeight,
			WorldSizeInVoxels = VoxelData.WorldSizeInChunks,
			BiomeAttributeData = world.BiomeAttributeData
		};

		populateVoxelMapHandle = new PopulateVoxelMapJob
		{
			Position = ChunkPosition,
			VoxelData = voxelMapData,
			VoxelMap = voxelMap
		}.Schedule();
	}

	private void CreateMeshDataJob()
	{
		meshData = new ChunkJob.MeshData()
		{
			MeshVertices = new NativeList<int3>(Allocator.TempJob),
			MeshTriangles = new NativeList<int>(Allocator.TempJob),
			MeshUVs = new NativeList<float2>(Allocator.TempJob)
		};

		chunkJobHandle = new ChunkJob
		{
			meshData = meshData,
			chunkData = new ChunkJob.ChunkData
			{
				VoxelMap = voxelMap,
				BlockTypes = world.BlockTypes,
				BiomeData = world.BiomeAttributeData
				
			},

			blockData = new ChunkJob.BlockData
			{
				BlockVertices = VoxelData.VoxelVertices,
				BlockTriangles = VoxelData.VoxelTriangles,
				BlockUVs = VoxelData.VoxelUVs,
				BlockFaceChecks = VoxelData.FaceChecks
			},

			ChunkHeight = VoxelData.ChunkHeight,
			ChunkWidth = VoxelData.ChunkWidth,
			TextureAtlasSize = VoxelData.TextureAtlasSizeInBlocks,
			NormalizedTextureAtlas = VoxelData.NormalizedBlockTextureSize,
			Position = new int3(ChunkPosition),
			WorldSizeInVoxels = VoxelData.WorldSizeInVoxels
		}.Schedule(populateVoxelMapHandle);
	}

	public void CreateMesh()
	{
		chunkJobHandle.Complete();

		Mesh mesh = new Mesh();
		//mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		mesh.MarkDynamic();

		mesh.vertices = meshData.MeshVertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray();
		mesh.triangles = meshData.MeshTriangles.ToArray();
		mesh.uv = meshData.MeshUVs.ToArray().Select(uvs => new Vector2(uvs.x, uvs.y)).ToArray();

		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		mesh.RecalculateTangents();
		mesh.RecalculateUVDistributionMetrics();
		mesh.Optimize();

		meshFilter.mesh = mesh;
		meshCollider.sharedMesh = meshFilter.sharedMesh;

		meshData.MeshTriangles.Dispose();
		meshData.MeshVertices.Dispose();
		meshData.MeshUVs.Dispose();
		voxelMap.Dispose();
	}

	public bool IsActive
	{
		get
		{
			return isActive;
		}
		set
		{
			isActive = value;
			if (chunkObject != null)
			{
				chunkObject.SetActive(value);
			}
		}
	}

	public bool IsCompleted
	{
		get
		{
			return chunkJobHandle.IsCompleted;
		}
	}

	public Vector3 ChunkPosition
	{
		get
		{
			return chunkObject.transform.position;
		}
	}
}