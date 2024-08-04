using System;
using UnityEngine;

namespace PatataStudio
{
	[CreateAssetMenu(menuName = "Minecraft/Voxel/Voxel Mesh Data", fileName = "New Voxel Mesh Data", order = 1)]
	public class VoxelMeshData : ScriptableObject
	{
		public FaceData[] FaceDatas;
		public Mesh mesh;

		public void OnValidate()
		{
			FaceDatas = null;

			if (mesh == null)
			{
				Debug.LogWarning("Mesh is not assigned.");
				return;
			}

			Vector3[] vertices = mesh.vertices;
			Vector2[] uvs = mesh.uv;
			int[] triangles = mesh.triangles;

			if (triangles.Length % 6 != 0)
			{
				Debug.LogWarning("The number of triangles is not divisible by 6, which is required to form quads.");
				return;
			}

			int quadCount = triangles.Length / 6;
			FaceDatas = new FaceData[quadCount];

			for (int i = 0; i < quadCount; i++)
			{
				FaceDatas[i] = new FaceData
				{
					Vertices = new VertexData[4]
				};

				// Each quad is formed by two triangles:
				// Triangle 1: vertices[0], vertices[1], vertices[2]
				// Triangle 2: vertices[2], vertices[1], vertices[3]
				int baseIndex = i * 6;
				int[] quadIndices = new int[]
				{
					triangles[baseIndex],     // Bottom-right
					triangles[baseIndex + 1], // Top-right
					triangles[baseIndex + 2], // Bottom-right
					triangles[baseIndex + 5]  // Bottom-left
				};

				for (int j = 0; j < 4; j++)
				{
					int vertexIndex = quadIndices[j];
					FaceDatas[i].Vertices[j] = new VertexData
					{
						Position = vertices[vertexIndex],
						UV = uvs[vertexIndex]
					};
				}
			}
		}

		[ContextMenu("Rebuild Mesh")]
		private void RebuildMesh()
		{
			FaceDatas = null;
			OnValidate();
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
	}
}