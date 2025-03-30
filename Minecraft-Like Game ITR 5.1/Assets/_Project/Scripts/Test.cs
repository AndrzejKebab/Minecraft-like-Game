using System.Linq;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Core;

namespace PatataGames
{
	public class Test : MonoBehaviour
	{
		public MeshData[] MeshDatas;
		public Material   Mat;

		private readonly NativeArray<VertexAttributeDescriptor> layout = new (2, Allocator.Persistent)
		                                                                 {
			                                                                 [0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
			                                                                 [1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
		                                                                 };

		[Button("Test Mesh")]
		public void TestMesh()
		{
			var pos = Vector3.zero;

			foreach (MeshData type in MeshDatas)
			{
				var go = new GameObject(type.name);
				go.transform.position =  pos;
				pos                   += new Vector3(2, 0, 0);

				var meshFilter   = go.AddComponent<MeshFilter>();
				var meshRenderer = go.AddComponent<MeshRenderer>();
				meshRenderer.material = Mat;

				meshFilter.sharedMesh = CreateMesh(type.FaceDatas);
			}
		}

		private Mesh CreateMesh(FaceData[] faceDatas)
		{
			var mesh = new Mesh();
			
			var triangles = new NativeList<uint>(Allocator.Persistent);
			var vertices  = new NativeList<Vertex>(Allocator.Persistent);
			var normals   = new NativeList<float3>(Allocator.Persistent);

			uint baseIndex = 0;

			// Iterate over each FaceData.
			foreach (FaceData faceData in faceDatas)
			{
				// Safety check: skip empty or null faces.
				if (faceData.IsEmpty() || faceData.Vertices == null || faceData.Vertices.Length < 3)
					continue;

				uint vertexCount = (uint)faceData.Vertices.Length;

				// Add the face's vertices, UVs, and a flat normal.
				for (int v = 0; v < vertexCount; v++)
				{
					vertices.Add(faceData.Vertices[v]);
					normals.Add(faceData.Normal);
				}

				// Triangulate differently depending on the vertex count.
				if (vertexCount % 4 == 0)
				{
					// Process as groups of quads.
					for (uint groupStart = 0; groupStart < vertexCount; groupStart += 4)
					{
						// For each quad, create two triangles:
						// Triangle 1: (0,1,2)
						triangles.Add(baseIndex + groupStart + 0);
						triangles.Add(baseIndex + groupStart + 1);
						triangles.Add(baseIndex + groupStart + 2);
						// Triangle 2: (0,2,3)
						triangles.Add(baseIndex + groupStart + 0);
						triangles.Add(baseIndex + groupStart + 2);
						triangles.Add(baseIndex + groupStart + 3);
					}
				}
				else if (vertexCount % 3 == 0)
				{
					// Assume the face is already triangulated.
					for (uint t = 0; t < vertexCount; t += 3)
					{
						triangles.Add(baseIndex + t);
						triangles.Add(baseIndex + t + 1);
						triangles.Add(baseIndex + t + 2);
					}
				}
				else
				{
					// Fallback: use a triangle fan for convex polygons.
					for (uint t = 2; t < vertexCount; t++)
					{
						triangles.Add(baseIndex + 0);
						triangles.Add(baseIndex + t - 1);
						triangles.Add(baseIndex + t);
					}
				}

				// Advance baseIndex for the next face.
				baseIndex += vertexCount;
			}
			
			// Configure a single submesh and recalculate bounds.
			mesh.SetVertexBufferParams(vertices.Length, layout);
			mesh.SetVertexBufferData(vertices.AsArray(), 0, 0, vertices.Length);
			mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
			mesh.SetIndexBufferData(triangles.AsArray(), 0, 0,triangles.Length);
			mesh.SetNormals(normals.AsArray());
			mesh.SetTangents(normals.AsArray().Select(normal => new Vector4(normal.x, normal.y, normal.z, 1)).ToArray());
			mesh.RecalculateBounds();
			mesh.subMeshCount = 1;
			mesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));
			
			vertices.Dispose();
			triangles.Dispose();
			normals.Dispose();
			mesh.name = "Mesh";
			return mesh;
		}
	}
}