using System;
using UnityEngine;

namespace PatataStudio
{
	[CreateAssetMenu(menuName = "Minecraft/Voxel/Voxel Mesh Data", fileName = "New Voxel Mesh Data", order = 1)]
	public class VoxelMeshData : ScriptableObject
	{
		public FaceData[] FaceDatas;
		[SerializeField] private Mesh mesh;

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

			if (triangles.Length % 3 != 0)
			{
				Debug.LogWarning("The number of triangles is not divisible by 3, which is required to form triangles.");
				return;
			}

			int triangleCount = triangles.Length / 3;
			FaceDatas = new FaceData[triangleCount];

			for (int i = 0; i < triangleCount; i++)
			{
				FaceDatas[i] = new FaceData
				{
					Vertices = new VertexData[3]
				};

				int baseIndex = i * 3;
				int[] triangleIndices = new int[]
				{
					triangles[baseIndex],
					triangles[baseIndex + 1],
					triangles[baseIndex + 2]
				};

				for (int j = 0; j < 3; j++)
				{
					int vertexIndex = triangleIndices[j];
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