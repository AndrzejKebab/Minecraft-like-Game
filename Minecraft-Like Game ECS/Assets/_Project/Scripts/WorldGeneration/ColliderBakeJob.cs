using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration
{
	[BurstCompile]
	public struct ColliderBakeJob : IJob
	{
		[ReadOnly] public UnityEngine.Mesh.MeshDataArray                             MeshDataArray;
		[WriteOnly] public            NativeArray<BlobAssetReference<Collider>> Collider;

		public void Execute()
		{
			UnityEngine.Mesh.MeshData     meshData  = MeshDataArray[0];
			SubMeshDescriptor sub0      = meshData.GetSubMesh(0);
			var               vertCount = meshData.vertexCount;
			var               idxCount  = sub0.indexCount;

			var verts = new NativeArray<float3>(vertCount,    Allocator.Temp);
			var tris  = new NativeArray<int3>(idxCount / 3,  Allocator.Temp);

			NativeArray<Vertex> rawVerts = meshData.GetVertexData<Vertex>(stream: 0);
			for (var j = 0; j < vertCount; j++)
				verts[j] = new float3(rawVerts[j].Position.x,
				                      rawVerts[j].Position.y,
				                      rawVerts[j].Position.z);

			if (meshData.indexFormat == IndexFormat.UInt16)
			{
				NativeArray<ushort> rawIdx = meshData.GetIndexData<ushort>();
				for (var j = 0; j < idxCount / 3; j++)
					tris[j] = new int3(rawIdx[j * 3], rawIdx[j * 3 + 1], rawIdx[j * 3 + 2]);
			}
			else
			{
				NativeArray<int> rawIdx = meshData.GetIndexData<int>();
				for (var j = 0; j < idxCount / 3; j++)
					tris[j] = new int3(rawIdx[j * 3], rawIdx[j * 3 + 1], rawIdx[j * 3 + 2]);
			}

			Collider[0] = MeshCollider.Create(verts, tris, CollisionFilter.Default, Material.Default);
			verts.Dispose();
			tris.Dispose();
		}
	}
}