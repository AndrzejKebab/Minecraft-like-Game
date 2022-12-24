using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
	[SerializeField] private MeshRenderer meshRenderer;
	[SerializeField] private MeshFilter meshFilter;

	private void Start()
	{
		int _vertexIndex = 0;
		List<Vector3> vertices = new List<Vector3>();
		List<int> triangles = new List<int>();
		List<Vector2> uvs = new List<Vector2>();

		for(int p = 0; p < 6; p++)
		{
			for (int i = 0; i < 6; i++)
			{
				int _triangleIndex = VoxelData.VoxelTriangles[p, i];
				vertices.Add(VoxelData.VoxelVerticles[_triangleIndex]);
				triangles.Add(_vertexIndex);

				uvs.Add(VoxelData.voxelUvs[i]);

				_vertexIndex++;
			}
		}

		Mesh mesh = new Mesh();
		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.uv = uvs.ToArray();

		mesh.RecalculateNormals();

		meshFilter.mesh = mesh;
	}
}
