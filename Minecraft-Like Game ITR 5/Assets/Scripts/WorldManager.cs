using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio
{
	public class WorldManager : Singleton<WorldManager>
	{
		[field: SerializeField] public Material[] Materials { get; private set; }
		[field: SerializeField] public VoxelType[] VoxelTypes { get; private set; }


		public Mesh testMesh;

		private void Start()
		{

		}

		[ContextMenu("Test")]
		[ContextMenu("Test")]
		public void Test()
		{
			var pos = new Vector3(0, 0, 0);
			foreach (VoxelType type in VoxelTypes)
			{
				var test = new GameObject(type.name);
				test.transform.position = pos;
				pos += new Vector3(1, 0, 0);

				var meshFilter = test.AddComponent<MeshFilter>();
				var meshRenderer = test.AddComponent<MeshRenderer>();
				meshRenderer.material = Materials[0];

				var mesh = new Mesh();

				var faceDatas = type.Voxel.FaceDatas;
				var vertices = new List<Vector3>();
				var uvs = new List<Vector2>();
				var triangles = new List<int>();
				var normals = new List<Vector3>();

				int index = 0;

				for (int i = 0; i < Math.Min(faceDatas.Length, 6); i++)
				{
					var faceData = faceDatas[i];
					var verticesPerFace = faceData.Vertices.Length;

					if (verticesPerFace % 4 != 0)
					{
						Debug.LogWarning($"Face {i} of voxel type {type.name} does not have a multiple of 4 vertices. Skipping this face.");
						continue;
					}

					// Add triangles (each face should have N triangles per N * 2 vertices)
					for (int j = 0; j < verticesPerFace; j += 4)
					{
						triangles.Add(index);
						triangles.Add(index + 1);
						triangles.Add(index + 2);
						triangles.Add(index);
						triangles.Add(index + 2);
						triangles.Add(index + 3);

						// Add vertices, UVs, and normals
						for (int k = 0; k < 4; k++)
						{
							vertices.Add(faceData.Vertices[j + k].Position);
							uvs.Add(faceData.Vertices[j + k].UV);
							normals.Add(faceData.Vertices[j + k].Normal);
						}

						index += 4;
					}
				}

				mesh.SetVertices(vertices);
				mesh.SetUVs(0, uvs);
				mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);
				mesh.SetNormals(normals);

				var desc = new SubMeshDescriptor(0, triangles.Count, MeshTopology.Triangles);
				mesh.subMeshCount = 1;
				mesh.SetSubMesh(0, desc);
				mesh.RecalculateUVDistributionMetrics();
				mesh.RecalculateTangents();

				meshFilter.sharedMesh = mesh;
			}
		}
	}
}