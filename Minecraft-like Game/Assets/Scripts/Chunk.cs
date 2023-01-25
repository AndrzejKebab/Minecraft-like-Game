using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;

	private NativeArray<bool> voxelMap;

	private JobHandle chunkJobHandle;
	private ChunkJob.MeshData meshData;


	private void Awake()
	{
		meshRenderer = GetComponent<MeshRenderer>();
		meshFilter = GetComponent<MeshFilter>();
	}

	private void Start()
	{
		voxelMap = new NativeArray<bool>(VoxelData.ChunkWidth * VoxelData.ChunkHeight * VoxelData.ChunkWidth, Allocator.TempJob);

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
					voxelMap[x + VoxelData.ChunkWidth * (y + VoxelData.ChunkHeight * z)] = true;
				}
			}
		}
	}

	private void CreateMeshDataJob()
	{
		meshData = new ChunkJob.MeshData()
		{
			MeshVertices = new NativeList<int3>(Allocator.TempJob),
			MeshTriangles = new NativeList<int>(Allocator.TempJob),
			MeshUVs = new NativeList<int2>(Allocator.TempJob)
		};

		chunkJobHandle = new ChunkJob
		{
			meshData = meshData,
			chunkData = new ChunkJob.ChunkData
			{
				VoxelMap = voxelMap
			},

			blockData = new ChunkJob.BlockData
			{
				BlockVertices = VoxelData.VoxelVertices,
				BlockTriangles = VoxelData.VoxelTriangles,
				BlockUVs = VoxelData.VoxelUVs,
				BlockFaceChecks = VoxelData.FaceChecks
			},

			ChunkHeight = VoxelData.ChunkHeight,
			ChunkWidth = VoxelData.ChunkWidth

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
		//mesh.Optimize();

		meshFilter.mesh = mesh;

		meshData.MeshTriangles.Dispose();
		meshData.MeshVertices.Dispose();
		meshData.MeshUVs.Dispose();
		voxelMap.Dispose();
	}

}