using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace PatataStudio
{
	public enum FaceSide
	{
		Front = 0,
		Back = 1,
		Right = 2,
		Left = 3,
		Top = 4,
		Bottom = 5
	}

	[Serializable]
	public struct FaceData
	{
		public Vector3 Normal;
		public Vertex[] Vertices;
	}

	[Serializable]
	public struct Vertex
	{
		public Vector3 Position;
		public Vector2 UV;
	}

	[CreateAssetMenu(menuName = "Minecraft/Voxel/Voxel Mesh Data", fileName = "New Voxel Mesh Data", order = 1)]
	public class VoxelMeshData : ScriptableObject
	{
		public const byte MaxFaces = 6;

		public FaceData[] FaceDatas = new FaceData[MaxFaces];
		[SerializeField] private Mesh mesh;

		/*
		private void OnValidate()
		{
			if (mesh == null)
			{
				throw new ArgumentNullException(nameof(mesh), "Mesh is not assigned.");
			}

			if (!dontRebuildMesh) RebuildFaceDatas();
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

			List<FaceData> orderedFaceDatas = new List<FaceData>();
			Vector3[] directions = new Vector3[]
			{
				Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down
			};

			foreach (var direction in directions)
			{
				if (normalToTriangles.TryGetValue(direction, out List<int> triangleIndices))
				{
					HashSet<int> uniqueVertices = new HashSet<int>(triangleIndices);

					FaceData faceData = new FaceData
					{
						Vertices = new Vertex[uniqueVertices.Count],
						Normal = direction,
					};

					int vertexIndex = 0;
					foreach (int vertex in uniqueVertices)
					{
						faceData.Vertices[vertexIndex] = new Vertex
						{
							Position = vertices[vertex],
							UV = uvs[vertex]
						};
						vertexIndex++;
					}

					orderedFaceDatas.Add(faceData);
				}
			}

			// Assign orderedFaceDatas to FaceDatas
			for (int i = 0; i < orderedFaceDatas.Count && i < MaxFaces; i++)
			{
				FaceDatas[i] = orderedFaceDatas[i];
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
		*/
	}
}