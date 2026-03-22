using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
	public partial struct ChunkCollidersSystem : ISystem
	{
		private const int COLLIDER_RADIUS = 1;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
		}

		public void OnUpdate(ref SystemState state)
		{
			float3 playerPos   = SystemAPI.GetComponentRO<LocalTransform>(SystemAPI.GetSingletonEntity<Player>()).ValueRO.Position;
			int3   playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);
			var    ecb         = new EntityCommandBuffer(Allocator.TempJob);

			// Create Unity Physics Collider
			foreach ((RefRO<ChunkMeshData> meshData, RefRO<ChunkPositionComponent> pos, Entity entity) in SystemAPI
				         .Query<RefRO<ChunkMeshData>, RefRO<ChunkPositionComponent>>()
				         .WithAll<IsVisible, HasRenderMesh>().WithNone<HasCollider>().WithEntityAccess())
			{
				if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;

				var solidVerticesLength = meshData.ValueRO.SolidMesh.IsCreated ? meshData.ValueRO.SolidMesh.Vertices.Length : 0;
				var transparentVerticesLenght = meshData.ValueRO.TransparentMesh.IsCreated
					            ? meshData.ValueRO.TransparentMesh.Vertices.Length
					            : 0;

				var solidIndicesLenght = meshData.ValueRO.SolidMesh.IsCreated ? meshData.ValueRO.SolidMesh.Triangles.Length : 0;
				var transparentIndicesLenght = meshData.ValueRO.TransparentMesh.IsCreated
					            ? meshData.ValueRO.TransparentMesh.Triangles.Length
					            : 0;

				if (solidVerticesLength + transparentVerticesLenght <= 0) continue;
				var vertices                                = new NativeArray<float3>(solidVerticesLength + transparentVerticesLenght, Allocator.TempJob);
				for (var i = 0; i < solidVerticesLength; i++) vertices[i] = meshData.ValueRO.SolidMesh.Vertices[i].position;
				for (var i = 0; i < transparentVerticesLenght; i++)
					vertices[solidVerticesLength + i] = meshData.ValueRO.TransparentMesh.Vertices[i].position;

				var triangles = new NativeArray<int3>((solidIndicesLenght + transparentIndicesLenght) / 3, Allocator.TempJob);
				var triIndex  = 0;
				for (var i = 0; i < solidIndicesLenght; i += 3)
					triangles[triIndex++] = new int3(meshData.ValueRO.SolidMesh.Triangles[i],
					                                 meshData.ValueRO.SolidMesh.Triangles[i + 1],
					                                 meshData.ValueRO.SolidMesh.Triangles[i + 2]);
				for (var i = 0; i < transparentIndicesLenght; i += 3)
					triangles[triIndex++] = new int3(meshData.ValueRO.TransparentMesh.Triangles[i] + solidVerticesLength,
					                                 meshData.ValueRO.TransparentMesh.Triangles[i + 1] + solidVerticesLength,
					                                 meshData.ValueRO.TransparentMesh.Triangles[i + 2] + solidVerticesLength);

				BlobAssetReference<Collider> collider =
					MeshCollider.Create(vertices, triangles, CollisionFilter.Default);

				ecb.AddComponent(entity, new PhysicsCollider { Value         = collider });
				ecb.AddSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
				ecb.AddComponent<HasCollider>(entity);

				vertices.Dispose();
				triangles.Dispose();
			}

			// Cleanup Out-of-Range Colliders
			foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in SystemAPI.Query<RefRO<ChunkPositionComponent>>()
				         .WithAll<HasCollider>().WithEntityAccess())
				if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS))
				{
					ecb.RemoveComponent<PhysicsCollider>(entity);
					ecb.RemoveComponent<PhysicsWorldIndex>(entity);
					ecb.RemoveComponent<HasCollider>(entity);
				}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}

		private static bool IsChebyshevNear(int3 a, int3 b, int radius)
		{
			int3 d = math.abs(a - b);
			return d.x <= radius && d.y <= radius && d.z <= radius;
		}
	}
}