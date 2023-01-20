using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class Chunk
{
	private GameObject chunkObject;
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;
	private MeshCollider meshCollider;

	public ChunkCoord coord;

	private NativeArray<ushort> voxelMap = new NativeArray<ushort>(VoxelData.ChunkWidth * VoxelData.ChunkHeight * VoxelData.ChunkWidth, Allocator.TempJob);

	private JobHandle chunkJobHandle;
	private ChunkJob.MeshData meshData;

	private World world;

	public Chunk(ChunkCoord _coord, World _world)
	{
		world = _world;
		coord = _coord;

		chunkObject = new GameObject();

		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();
		meshCollider = chunkObject.AddComponent<MeshCollider>();

		meshRenderer.material = world.Material;
		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(coord.X * VoxelData.ChunkWidth, 0, coord.Z * VoxelData.ChunkWidth);
		chunkObject.name = $"Chunk [{coord.X}, {coord.Z}]";
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
					voxelMap[x + VoxelData.ChunkWidth * (y + VoxelData.ChunkHeight * z)] = world.GetVoxel(new Vector3(x, y, z) + position);
				}
			}
		}
	}

	public bool IsActive
	{
		get
		{
			return chunkObject.activeSelf;
		}
		set
		{
			chunkObject.SetActive(value);
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
				BlockTypes = world.BlockTypes
				
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
			Position = math.int3(position),
			WorldSizeInVoxels = VoxelData.WorldSizeInVoxels
		}.Schedule();
	}

	private void CreateMesh()
	{
		chunkJobHandle.Complete();

		Mesh mesh = new Mesh();
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

public class ChunkCoord
{
	public int X;
	public int Z;

	public ChunkCoord(int x, int z)
	{
		X = x;
		Z = z;
	}

	public bool Equals(ChunkCoord other)
	{
		if (other == null)
		{
			return false;
		}
		else if(other.X == X && other.Z == Z)
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}