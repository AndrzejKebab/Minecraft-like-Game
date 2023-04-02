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

	public int2 coord;

	private NativeArray<short> voxelMap = new NativeArray<short>(VoxelData.ChunkWidth * VoxelData.ChunkHeight * VoxelData.ChunkWidth, Allocator.TempJob);

	private JobHandle chunkJobHandle;
	private ChunkJob.MeshData meshData;

	private World world;

	private bool isActive;

	public Chunk(int2 _coord, World _world)
	{
		world = _world;
		coord = _coord;
		isActive = true;

		Initialise();
	}

	private void Initialise()
	{
		chunkObject = new GameObject();

		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();
		meshCollider = chunkObject.AddComponent<MeshCollider>();

		meshRenderer.material = world.Material;
		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(coord.x * VoxelData.ChunkWidth, 0, coord.y * VoxelData.ChunkWidth);
		chunkObject.name = $"Chunk [{coord.x}, {coord.y}]";
		chunkObject.layer = LayerMask.NameToLayer("Chunk");

		PopulateVoxelMap();
		CreateMeshDataJob();

		CreateMesh();
	}

	private void PopulateVoxelMap()
	{
		for (int y = 0; y < VoxelData.ChunkHeight; y++)
		{
			for (int x = 0; x < VoxelData.ChunkWidth; x++)
			{
				for (int z = 0; z < VoxelData.ChunkWidth; z++)
				{
					voxelMap[x + VoxelData.ChunkWidth * (y + VoxelData.ChunkHeight * z)] = WorldExtensions.GetVoxel(new Vector3(x, y, z) + position, VoxelData.ChunkHeight, VoxelData.WorldSizeInVoxels, world.BiomeAttributeData);
				}
			}
		}
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

	public Vector3 position
	{
		get
		{
			return chunkObject.transform.position;
		}
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
			Position = new int3(position),
			WorldSizeInVoxels = VoxelData.WorldSizeInVoxels
		}.Schedule();
	}

	private void CreateMesh()
	{
		chunkJobHandle.Complete();

		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
}