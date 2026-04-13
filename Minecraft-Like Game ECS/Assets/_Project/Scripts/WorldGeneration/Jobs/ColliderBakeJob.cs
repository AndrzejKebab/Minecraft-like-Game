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
		[ReadOnly] public NativeArray<Vertex> SolidVertices;
		[ReadOnly] public NativeArray<int>    SolidIndices;[WriteOnly] public NativeArray<BlobAssetReference<Collider>> Collider;
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
				var data1 = SolidVertices[j].Data1;
				var px    = (data1 & 0x3FF) / 10f;
				var py    = ((data1 >> 10) & 0x3FF) / 10f;
				var pz    = ((data1 >> 20) & 0x3FF) / 10f;
				
				verts[j] = new float3(px, py, pz);
			}

			for (var j = 0; j < idxCount / 3; j++)
				tris[j] = new int3(SolidIndices[j * 3], SolidIndices[j * 3 + 1], SolidIndices[j * 3 + 2]);

			Collider[0] = MeshCollider.Create(verts, tris, Filter, Material.Default);
			verts.Dispose();
			tris.Dispose();
		}
	}
}