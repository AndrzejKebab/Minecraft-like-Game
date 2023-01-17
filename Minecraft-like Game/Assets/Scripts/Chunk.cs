using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
	private  MeshFilter meshFilter;

	private void Awake()
	{
		meshFilter = GetComponent<MeshFilter>();
	}

	private void Start()
	{
		var _position = transform.position;

		var blocks = new NativeArray<Block>(4096, Allocator.TempJob);

		for(int x = 0; x < 16; x++)
		{
			for (int z = 0; z < 16; z++)
			{
				var y = Mathf.FloorToInt(Mathf.PerlinNoise((_position.x + x) * 0.15f, (_position.z + z) * 0.15f) * 16);

				for (int i = 0; i < y; i++)
				{
					blocks[BlockExtensions.GetBlockIndex(new int3(x, i, z))] = Block.Stone;
				}

				for (int i = y; i < 16; i++)
				{
					blocks[BlockExtensions.GetBlockIndex(new int3(x, i, z))] = Block.Air;
				}
			}
		}

		var meshData = new ChunkJob.MeshData()
		{
			Vertices = new NativeList<int3>(Allocator.TempJob),
			Triangles = new NativeList<int>(Allocator.TempJob)
		};

		var jobHandle = new ChunkJob
		{
			meshData = meshData,
			chunkData = new ChunkJob.ChunkData
			{
				Blocks = blocks
			},
			blockData = new ChunkJob.BlockData
			{
				Vertices = VoxelData.Vertices,
				Triangles = VoxelData.Triangles
			}
		}.Schedule();

		jobHandle.Complete();

		Mesh mesh = new Mesh();
		mesh.MarkDynamic();
		mesh.vertices = meshData.Vertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray();
		mesh.triangles = meshData.Triangles.ToArray();
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		mesh.RecalculateTangents();
		mesh.RecalculateUVDistributionMetrics();
		mesh.Optimize();

		meshFilter.mesh = mesh;

		meshData.Triangles.Dispose();
		meshData.Vertices.Dispose();
		blocks.Dispose();
	}
}
