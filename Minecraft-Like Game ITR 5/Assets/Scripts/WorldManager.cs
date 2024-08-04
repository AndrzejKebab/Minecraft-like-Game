using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio
{
	public class WorldManager : Singleton<WorldManager>
	{
		[field:SerializeField] public Material[] Materials { get; private set; }
		[field:SerializeField] public VoxelType[] VoxelTypes { get; private set; }

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

				for (int i = 0; i < 6; i++)
				{
					var faceData = faceDatas[i];

					triangles.Add(index);
					triangles.Add(index + 1);
					triangles.Add(index + 2);
					triangles.Add(index);
					triangles.Add(index + 2);
					triangles.Add(index + 3);

					vertices.Add(faceData.Vertices[0].Position);
					vertices.Add(faceData.Vertices[1].Position);
					vertices.Add(faceData.Vertices[2].Position);
					vertices.Add(faceData.Vertices[3].Position);

					uvs.Add(faceData.Vertices[0].UV);
					uvs.Add(faceData.Vertices[1].UV);
					uvs.Add(faceData.Vertices[2].UV);
					uvs.Add(faceData.Vertices[3].UV);

					index += 4;
				}

				mesh.SetVertices(vertices.ToArray());
				mesh.SetUVs(0, uvs);
				mesh.SetIndices(triangles, MeshTopology.Triangles, 0);

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