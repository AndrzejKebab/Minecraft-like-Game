using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class Chunk
{
	[SerializeField] private ChunkCoord chunkCoord;

	private GameObject chunkObject;
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;

	private int vertexIndex = 0;
	private List<Vector3> vertices = new List<Vector3>();
	private List<int> triangles = new List<int>();
	private List<Vector2> uvs = new List<Vector2>();

	private byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

	private World world;

	public Chunk (ChunkCoord _chunkCoord, World _world)
	{
		chunkCoord = _chunkCoord;
		world = _world;
		chunkObject = new GameObject();
		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();

		meshRenderer.material = world.Material;
		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(chunkCoord.x * VoxelData.ChunkWidth, 0f, chunkCoord.z * VoxelData.ChunkWidth);
		chunkObject.name = "Chunk " + chunkCoord.x + " , " + chunkCoord.z;

		PopulateVoxelMap();

		CreateMeshData();

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
					if(y < 1)
					{
						voxelMap[x, y, z] = 1;
					}
					else if(y == VoxelData.ChunkHeight - 1)
					{
						voxelMap[x, y, z] = 2;
					}
					else if (y >= VoxelData.ChunkHeight - 6 && y < VoxelData.ChunkHeight - 1)
					{
						voxelMap[x, y, z] = 3;
					}
					else
					{
						voxelMap[x, y, z] = 4;
					}
					
				}
			}
		}
	}

	private void CreateMeshData()
	{
		for (int y = 0; y < VoxelData.ChunkHeight; y++)
		{
			for (int x = 0; x < VoxelData.ChunkWidth; x++)
			{
				for (int z = 0; z < VoxelData.ChunkWidth; z++)
				{
					AddVoxelDataToChunk(new Vector3(x, y, z));
				}
			}
		}
	}

	private bool IsVoxelInChunk(int x, int y, int z)
	{
		if (x < 0 || x > VoxelData.ChunkWidth - 1 || y < 0 || y > VoxelData.ChunkHeight - 1 || z < 0 || z > VoxelData.ChunkWidth - 1)
		{
			return false;
		}
		else
		{
			return true;
		}

	}

	private bool CheckVoxel(Vector3 position)
	{
		int x = Mathf.FloorToInt(position.x);
		int y = Mathf.FloorToInt(position.y);
		int z = Mathf.FloorToInt(position.z);

		if(!IsVoxelInChunk(x, y, z))
		{
			return false;
		}

		return world.BlockTypes[voxelMap[x, y, z]].isSolid;
	}

	private void AddVoxelDataToChunk(Vector3 position)
	{
		for (int face = 0; face < 6; face++)
		{
			if (!CheckVoxel(position + VoxelData.FaceChecks[face]))
			{
				byte blockID = voxelMap[(int)position.x, (int)position.y, (int)position.z];

				vertices.Add(position + VoxelData.VoxelVerticles[VoxelData.VoxelTriangles[face, 0]]);
				vertices.Add(position + VoxelData.VoxelVerticles[VoxelData.VoxelTriangles[face, 1]]);
				vertices.Add(position + VoxelData.VoxelVerticles[VoxelData.VoxelTriangles[face, 2]]);
				vertices.Add(position + VoxelData.VoxelVerticles[VoxelData.VoxelTriangles[face, 3]]);
				triangles.Add(vertexIndex);
				triangles.Add(vertexIndex + 1);
				triangles.Add(vertexIndex + 2);
				triangles.Add(vertexIndex + 2);
				triangles.Add(vertexIndex + 1);
				triangles.Add(vertexIndex + 3);
				vertexIndex += 4;

				AddTexture(world.BlockTypes[blockID].GetTextureID(face));
			}
		}
	}

	private void CreateMesh()
	{
		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.uv = uvs.ToArray();

		mesh.RecalculateNormals();

		meshFilter.mesh = mesh;
	}

	private void AddTexture(int textureID)
	{
		float y = textureID / VoxelData.TextureAtlasSizeInBlocks;
		float x = textureID - (y * VoxelData.TextureAtlasSizeInBlocks);

		x *= VoxelData.NormalizedBlockTextureSize;
		y *= VoxelData.NormalizedBlockTextureSize;

		y = 1f - y - VoxelData.NormalizedBlockTextureSize;

		uvs.Add(new Vector2(x, y));
		uvs.Add(new Vector2(x, y + VoxelData.NormalizedBlockTextureSize));
		uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y));
		uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y + VoxelData.NormalizedBlockTextureSize));
	}
}

public class ChunkCoord
{
	public int x;
	public int z;

	public ChunkCoord (int _x, int _z)
	{
		x = _x;
		z = _z;
	}
}