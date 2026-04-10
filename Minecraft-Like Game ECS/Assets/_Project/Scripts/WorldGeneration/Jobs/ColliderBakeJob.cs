using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace _Project.WorldGeneration.Jobs
{
	[BurstCompile]
	public struct ColliderBakeJob : IJob
	{
		[ReadOnly] public NativeList<Vertex> SolidVertices;
		[ReadOnly] public NativeList<int>    SolidIndices;

		[WriteOnly] public NativeArray<BlobAssetReference<Collider>> Collider;
		[ReadOnly]  public CollisionFilter                           Filter;

		public void Execute()
		{
			var vertCount = SolidVertices.Length;
			var idxCount  = SolidIndices.Length;
			if (idxCount == 0) return;

			var verts = new NativeArray<float3>(vertCount, Allocator.Temp);
			var tris  = new NativeArray<int3>(idxCount / 3, Allocator.Temp);

			for (var j = 0; j < vertCount; j++)
			{
				half4 p = SolidVertices[j].Position;
				verts[j] = new float3(p.x, p.y, p.z);
			}

			for (var j = 0; j < idxCount / 3; j++)
				tris[j] = new int3(SolidIndices[j * 3], SolidIndices[j * 3 + 1], SolidIndices[j * 3 + 2]);

			Collider[0] = MeshCollider.Create(verts, tris, Filter, Material.Default);
			verts.Dispose();
			tris.Dispose();
		}
	}
}