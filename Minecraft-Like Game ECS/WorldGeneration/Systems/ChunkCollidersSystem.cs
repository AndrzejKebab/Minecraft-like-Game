using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
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

			int bakedThisFrame = 0;

			foreach ((ChunkMeshData meshData, RefRO<ChunkPositionComponent> pos, Entity entity) in SystemAPI
				         .Query<ChunkMeshData, RefRO<ChunkPositionComponent>>()
				         .WithAll<IsVisible, HasRenderMesh>().WithNone<HasCollider>().WithEntityAccess())
			{
				if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;
				if (meshData.ChunkMesh == null || meshData.ChunkMesh.vertexCount == 0) continue;
				
				if (bakedThisFrame >= 1) continue; 

				BlobAssetReference<Collider> collider =
					MeshCollider.Create(meshData.ChunkMesh, CollisionFilter.Default, Material.Default);

				ecb.AddComponent(entity, new PhysicsCollider { Value         = collider });
				ecb.AddSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
				ecb.AddComponent<HasCollider>(entity);
				bakedThisFrame++;
			}

			foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in SystemAPI.Query<RefRO<ChunkPositionComponent>>()
				         .WithAll<HasCollider>().WithEntityAccess())
			{
				if (IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;
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