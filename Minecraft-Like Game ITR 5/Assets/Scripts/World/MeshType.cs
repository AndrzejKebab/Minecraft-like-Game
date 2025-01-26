using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace PatataGames
{
	public class MeshType : ScriptableObject
	{
		[field: SerializeField] public  FaceData[] FaceDatas { get; private set; }
		[SerializeField]        private Mesh       mesh;

		private void OnValidate()
		{
			if (mesh == null) throw new ArgumentNullException(nameof(mesh), "Mesh is not assigned.");
			RebuildFaceDatas();
		}

		private void RebuildFaceDatas()
		{
			FaceDatas = new FaceData[6];
			Vector3[] vertices  = mesh.vertices;
			Vector2[] uvs       = mesh.uv;
			Vector3[] normals   = mesh.normals;
			var       triangles = mesh.triangles;

			ClampAndRoundUVs(uvs);

			if (triangles.Length % 3 != 0)
				throw new
					ArgumentException("The number of triangles is not divisible by 3, which is required to form triangles.");

			var normalToTriangles = new Dictionary<Vector3, List<int>>();
			for (var i = 0; i < triangles.Length; i += 3)
			{
				Vector3 averageNormal =
					(normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]) / 3f;

				if (!normalToTriangles.TryGetValue(averageNormal, out List<int> triangleIndices))
				{
					triangleIndices = new List<int>();
					normalToTriangles.Add(averageNormal, triangleIndices);
				}

				triangleIndices.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
			}

			var orderedFaceDatas = new List<FaceData>();
			Vector3[] directions = new[]
			                       {
				                       Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up,
				                       Vector3.down
			                       };

			foreach (Vector3 direction in directions)
				if (normalToTriangles.TryGetValue(direction, out List<int> triangleIndices))
				{
					var uniqueVertices = new HashSet<int>(triangleIndices);

					var faceData = new FaceData
					               {
						               Vertices = new Vertex[uniqueVertices.Count],
						               Normal   = direction
					               };

					var vertexIndex = 0;
					foreach (var vertex in uniqueVertices)
					{
						faceData.Vertices[vertexIndex] = new Vertex
						                                 {
							                                 Position = vertices[vertex],
							                                 UV       = uvs[vertex]
						                                 };
						vertexIndex++;
					}

					orderedFaceDatas.Add(faceData);
				}

			// Assign orderedFaceDatas to FaceDatas
			for (var i = 0; i < orderedFaceDatas.Count && i < 6; i++) FaceDatas[i] = orderedFaceDatas[i];
		}

		private void ClampAndRoundUVs(Vector2[] uvs)
		{
			for (var i = 0; i < uvs.Length; i++)
			{
				uvs[i] = math.clamp(uvs[i], Vector2.zero, Vector2.one);
				uvs[i] = math.round(uvs[i] * 10) / 10;
			}
		}
	}
}