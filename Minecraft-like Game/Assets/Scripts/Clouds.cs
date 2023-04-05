using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using System.Linq;

public class Clouds : MonoBehaviour
{
	[SerializeField] private Material material;
	private int cloudHeight = 384;

	bool[,] cloudData = new bool[256, 256];
	List<float3> vertices = new List<float3>();
	List<int> triangles =	new List<int>();
	List<float3> normals =	new List<float3>();
	MeshFilter meshFilter;
	Mesh mesh;

	private int vertCount;

	void Start()
	{
		this.transform.position = new Vector3(0 - 128, cloudHeight, 0 - 128);
		mesh = new Mesh();
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.material = material;
		meshFilter = gameObject.AddComponent<MeshFilter>();
	}

	// Update is called once per frame
	void Update()
	{
		LoadCloudData();
		meshFilter.mesh = GetCloudMesh();
	}

	private void LoadCloudData()
	{
		for(int x = 0; x < 256; x++)
		{
			for(int z = 0; z < 256; z++)
			{
				if(NoiseGenerator.Get2DPerlin(x + Time.timeSinceLevelLoad, z + Time.timeSinceLevelLoad, 0, 0, 25) > 0.65f)
				{
					cloudData[x, z] = true;
				}
				else
				{
					cloudData[x, z] = false;
				}
			}
		}
	}

	private Mesh GetCloudMesh()
	{
		mesh.Clear();
		mesh.MarkDynamic();
		vertices.Clear();
		triangles.Clear();
		normals.Clear();
		vertCount = 0;

		for (int x = 0; x < 256; x++)
		{
			for (int z = 0; z < 256; z++)
			{
				if (cloudData[x, z])
				{
					AddCloudMeshData(x, z);
				}
			}
		}

		mesh.vertices = vertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.normals = normals.ToArray().Select(normal => new Vector3(normal.x, normal.y, normal.z)).ToArray();

		mesh.Optimize();

		return mesh;
	}

	private void AddCloudMeshData(int x, int z)
	{
		vertices.Add(new float3(x, 0, z));
		vertices.Add(new float3(x, 0, z + 1));
		vertices.Add(new float3(x + 1, 0, z + 1));
		vertices.Add(new float3(x + 1, 0, z));

		for(int i = 0; i < 4; i++)
		{
			normals.Add(Vector3.down);
		}

		triangles.Add(vertCount + 1);
		triangles.Add(vertCount);
		triangles.Add(vertCount + 2);
		triangles.Add(vertCount + 2);
		triangles.Add(vertCount);
		triangles.Add(vertCount + 3);

		vertCount += 4;
	}
}