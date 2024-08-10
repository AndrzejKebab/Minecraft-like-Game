using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio
{
	public class WorldManager : Singleton<WorldManager>
	{
		[field: SerializeField] public int Seed { get; private set; }
		[field: SerializeField] public string EncodedContinentalnessTree { get; private set; }
		[field: SerializeField] public string EncodedErosionTree { get; private set; }
		[field: SerializeField] public string EncodedPeaksAndValleysTree { get; private set; }
		[field: SerializeField] public string EncodedCavesTree { get; private set; }

		[field: SerializeField] public Material[] Materials { get; private set; }
		[field: SerializeField] public VoxelType[] VoxelTypes { get; private set; }

		private void Start()
		{
			// Initialization if needed.
		}

		[ContextMenu("Test")]
		public void Test()
		{
			var pos = new Vector3(0, 0, 0);

			// Iterate over each VoxelType.
			foreach (VoxelType type in VoxelTypes)
			{
				if(!type.GetVoxel().IsSolid) continue;

				var test = new GameObject(type.name);
				test.transform.position = pos;
				pos += new Vector3(2, 0, 0);

				var meshFilter = test.AddComponent<MeshFilter>();
				var meshRenderer = test.AddComponent<MeshRenderer>();
				meshRenderer.material = Materials[0];

				// Generate the mesh and assign it to the MeshFilter.
				meshFilter.sharedMesh = CreateMesh(type.GetVoxel().FaceDatas);
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

			// Iterate over each FaceData.
			for(int i = 0; i < faceDatas.Length; i++)
			{
				var faceData = faceDatas[i];
				var verticesPerFace = faceData.Vertices.Length;

				// Add vertices, UVs, and normals.
				for (int j = 0; j < verticesPerFace; j++)
				{
					var vertexData = faceData.Vertices[j];
					vertices.Add(vertexData.Position);
					uvs.Add(vertexData.UV);
					normals.Add(faceData.Normal);
				}

				if(verticesPerFace % 4 == 0)
				{
					// Calculate the number of groups of 4 vertices.
					int groups = verticesPerFace / 4;

					// Add triangles for each group of 4 vertices.
					for (int l = 0; l < groups; l++)
					{
						int start = l * 4;

						// First triangle (0, 1, 2).
						triangles.Add(index + start + 0);
						triangles.Add(index + start + 1);
						triangles.Add(index + start + 2);
						// Second triangle (2, 1, 3).
						triangles.Add(index + start + 2);
						triangles.Add(index + start + 1);
						triangles.Add(index + start + 3);
					}
				}
				else
				{
					for (int l = 0; l < verticesPerFace / 3; l++)
					{
						triangles.Add(index + l * 3);
						triangles.Add(index + l * 3 + 1);
						triangles.Add(index + l * 3 + 2);

					}
				}				

				index += verticesPerFace;
			}

			// Set mesh properties.
			mesh.SetVertices(vertices);
			mesh.SetUVs(0, uvs);
			mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);
			mesh.SetNormals(normals);
			mesh.SetTangents(normals.Select(normal => new Vector4(normal.x, normal.y, normal.z, 0)).ToArray());			

			// Create submesh and recalculate tangents.
			var desc = new SubMeshDescriptor(0, triangles.Count, MeshTopology.Triangles);
			mesh.subMeshCount = 1;
			mesh.SetSubMesh(0, desc);
			mesh.RecalculateUVDistributionMetrics();

			return mesh;
		}
	}	
}