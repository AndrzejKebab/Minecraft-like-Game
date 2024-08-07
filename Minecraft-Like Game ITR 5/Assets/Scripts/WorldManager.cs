using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio
{
	public class WorldManager : Singleton<WorldManager>
	{
		[field: SerializeField] public Material[] Materials { get; private set; }
		[field: SerializeField] public VoxelType[] VoxelTypes { get; private set; }

		private void Start()
		{
			// Initialization if needed
		}

		[ContextMenu("Test")]
		public void Test()
		{
			var pos = new Vector3(0, 0, 0);

			// Iterate over each VoxelType
			foreach (VoxelType type in VoxelTypes)
			{
				var test = new GameObject(type.name);
				test.transform.position = pos;
				pos += new Vector3(2, 0, 0);

				var meshFilter = test.AddComponent<MeshFilter>();
				var meshRenderer = test.AddComponent<MeshRenderer>();
				meshRenderer.material = Materials[0];

				// Generate the mesh and assign it to the MeshFilter
				meshFilter.sharedMesh = CreateMesh(type.Voxel.FaceDatas);
			}
		}

		private Mesh CreateMesh(FaceData[] faceDatas)
		{
			var mesh = new Mesh();

			var vertices = new List<Vector3>();
			var uvs = new List<Vector2>();
			var triangles = new List<int>();
			var normals = new List<Vector3>();

			int index = 0;

			// Iterate over each FaceData
			for(int i = 0; i < faceDatas.Length; i++)
			{
				//if (i % 2 == 0) continue;
				var faceData = faceDatas[i];
				var verticesPerFace = faceData.Vertices.Length;

				// Add vertices, UVs, and normals
				for (int j = 0; j < faceData.Vertices.Length; j++)
				{
					var vertexData = faceData.Vertices[j];
					vertices.Add(vertexData.Position);
					uvs.Add(vertexData.UV);
					normals.Add(faceData.Normal);
				}

				// Add triangle indices
				for (int l = 0; l < verticesPerFace - 2; l++)
				{
					triangles.Add(index);
					triangles.Add(index + l + 1);
					triangles.Add(index + l + 2);
				}

				index += verticesPerFace;
			}

			// Set mesh properties
			mesh.SetVertices(vertices);
			mesh.SetUVs(0, uvs);
			mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);
			mesh.SetNormals(normals);

			// Create submesh and recalculate tangents
			var desc = new SubMeshDescriptor(0, triangles.Count, MeshTopology.Triangles);
			mesh.subMeshCount = 1;
			mesh.SetSubMesh(0, desc);
			mesh.RecalculateUVDistributionMetrics();
			mesh.RecalculateTangents();

			return mesh;
		}
	}
}