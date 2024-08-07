using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace PatataStudio
{
	[CreateAssetMenu(menuName = "Minecraft/Voxel/Voxel Mesh Data", fileName = "New Voxel Mesh Data", order = 1)]
	public class VoxelMeshData : ScriptableObject
	{
		public const byte MaxFaces = 6;

		public FaceData[] FaceDatas = new FaceData[MaxFaces];
		[SerializeField] private Mesh mesh;

		private void OnValidate()
		{
			if (mesh == null)
			{
				throw new ArgumentNullException(nameof(mesh), "Mesh is not assigned.");
			}

			RebuildFaceDatas();
		}

		[ContextMenu("Rebuild Face Datas")]
		private void RebuildFaceDatas()
		{
			FaceDatas = new FaceData[MaxFaces];
			Vector3[] vertices = mesh.vertices;
			Vector2[] uvs = mesh.uv;
			Vector3[] normals = mesh.normals;
			int[] triangles = mesh.triangles;

			ClampAndRoundUVs(uvs);

			if (triangles.Length % 3 != 0)
			{
				throw new ArgumentException("The number of triangles is not divisible by 3, which is required to form triangles.");
			}

			Dictionary<Vector3, List<int>> normalToTriangles = new Dictionary<Vector3, List<int>>();

			for (int i = 0; i < triangles.Length; i += 3)
			{
				Vector3 averageNormal = (normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]) / 3f;

				if (!normalToTriangles.TryGetValue(averageNormal, out List<int> triangleIndices))
				{
					triangleIndices = new List<int>();
					normalToTriangles.Add(averageNormal, triangleIndices);
				}

				triangleIndices.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
			}

			int faceIndex = 0;
			foreach (var kvp in normalToTriangles)
			{
				if (faceIndex >= MaxFaces)
				{
					break;
				}

				List<int> triangleIndices = kvp.Value;
				HashSet<int> uniqueVertices = new HashSet<int>(triangleIndices);

				FaceData faceData = new FaceData
				{
					Vertices = new VertexData[uniqueVertices.Count]
				};

				int vertexIndex = 0;
				foreach (int vertex in uniqueVertices)
				{
					faceData.Vertices[vertexIndex] = new VertexData
					{
						Position = vertices[vertex],
						UV = uvs[vertex],
						Normal = normals[vertex]
					};
					vertexIndex++;
				}

				FaceDatas[faceIndex] = faceData;
				faceIndex++;
			}
		}

		private void ClampAndRoundUVs(Vector2[] uvs)
		{
			for (int i = 0; i < uvs.Length; i++)
			{
				uvs[i] = math.clamp(uvs[i], Vector2.zero, Vector2.one);
				uvs[i] = math.round(uvs[i] * 10) / 10;
			}
		}

		private int GetNormalIndex(Vector3 normal, Vector3[] normals)
		{
			Dictionary<Vector3, List<int>> normalToTriangles = new Dictionary<Vector3, List<int>>();

			for (int i = 0; i < normals.Length; i++)
			{
				Vector3 averageNormal = normals[i];

				bool foundSimilarNormal = false;
				foreach (var key in normalToTriangles.Keys)
				{
					if (Vector3.Angle(key, averageNormal) < 0.1f)
					{
						averageNormal = key;
						foundSimilarNormal = true;
						break;
					}
				}

				if (!foundSimilarNormal)
				{
					normalToTriangles[averageNormal] = new List<int>();
				}

				normalToTriangles[averageNormal].Add(i);
			}

			for (int i = 0; i < normalToTriangles.Keys.Count; i++)
			{
				if (Vector3.Angle(normal, normalToTriangles.Keys.ElementAt(i)) < 0.1f)
				{
					return i;
				}
			}
			return -1;
		}
	}
}

[Serializable]
public struct FaceData
{
	public VertexData[] Vertices;
}

[Serializable]
public struct VertexData
{
	public Vector3 Position;
	public Vector2 UV;
	public Vector3 Normal;
}