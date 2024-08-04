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
				var index = 0;

				for (int i = 0; i < faceDatas.Length; i++)
				{
					var faceData = faceDatas[i];

					// Add triangles (each face should have 3 vertices per triangle)
					for (int j = 0; j < 3; j++)
					{
						triangles.Add(index + j);
					}

					// Add vertices and UVs
					for (int j = 0; j < 3; j++)
					{
						vertices.Add(faceData.Vertices[j].Position);
						uvs.Add(faceData.Vertices[j].UV);
					}

					index += 3;
				}

				mesh.SetVertices(vertices);
				mesh.SetUVs(0, uvs);
				mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);

				var desc = new SubMeshDescriptor(0, triangles.Count, MeshTopology.Triangles);
				mesh.subMeshCount = 1;
				mesh.SetSubMesh(0, desc);
				mesh.RecalculateUVDistributionMetrics();
				mesh.RecalculateNormals();
				mesh.RecalculateTangents();

				meshFilter.sharedMesh = mesh;
			}
		}
	}
}