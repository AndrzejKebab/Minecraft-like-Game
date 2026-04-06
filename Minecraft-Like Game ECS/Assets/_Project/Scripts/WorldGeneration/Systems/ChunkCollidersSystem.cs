using System.Collections.Generic;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using Mesh = UnityEngine.Mesh;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
	public partial class ChunkCollidersSystem : SystemBase
	{
		private const           int  COLLIDER_RADIUS = 1;
		private static readonly uint chunkLayer      = (uint)(1 << LayerMask.NameToLayer("Chunk"));

		private static readonly CollisionFilter chunkFilter = new()
		                                                      {
			                                                      BelongsTo    = chunkLayer,
			                                                      CollidesWith = ~0u // Collide with everything
		                                                      };

		private readonly List<PendingBake> pendingBakes = new();

		protected override void OnCreate()
		{
			RequireForUpdate<Player>();
		}

		protected override void OnDestroy()
		{
			foreach (PendingBake b in pendingBakes)
			{
				b.Handle.Complete();
				if (!b.Collider.IsCreated) continue;
				if (b.Collider[0].IsCreated) b.Collider[0].Dispose();
				b.Collider.Dispose();
			}
		}

		protected override void OnUpdate()
		{
			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
			                                                            SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);
			var  ecb         = new EntityCommandBuffer(Allocator.Temp);

			for (var i = pendingBakes.Count - 1; i >= 0; i--)
			{
				PendingBake b = pendingBakes[i];
				if (!b.Handle.IsCompleted) continue;
				b.Handle.Complete();
				b.MeshDataArray.Dispose();

				if (EntityManager.Exists(b.Entity))
				{
					if (b.Collider[0].IsCreated)
					{
						if (EntityManager.HasComponent<PhysicsCollider>(b.Entity))
						{
							var oldCollider = EntityManager.GetComponentData<PhysicsCollider>(b.Entity);
							if (oldCollider.Value.IsCreated) oldCollider.Value.Dispose();
							ecb.SetComponent(b.Entity, new PhysicsCollider { Value = b.Collider[0] });
						}
						else
						{
							ecb.AddComponent(b.Entity, new PhysicsCollider { Value         = b.Collider[0] });
							ecb.AddSharedComponent(b.Entity, new PhysicsWorldIndex { Value = 0 });
							ecb.AddComponent<HasCollider>(b.Entity);
						}
					}
					else
					{
						if (EntityManager.HasComponent<PhysicsCollider>(b.Entity))
						{
							var oldCollider = EntityManager.GetComponentData<PhysicsCollider>(b.Entity);
							if (oldCollider.Value.IsCreated) oldCollider.Value.Dispose();
							ecb.RemoveComponent<PhysicsCollider>(b.Entity);
							ecb.RemoveComponent<HasCollider>(b.Entity);
						}
					}

					ecb.RemoveComponent<NeedsColliderSync>(b.Entity);
				}
				else
				{
					if (b.Collider[0].IsCreated) b.Collider[0].Dispose();
				}

				b.Collider.Dispose();
				pendingBakes.RemoveAt(i);
			}

			var scheduledThisFrame = 0;

			foreach ((ChunkMeshData meshData, RefRO<ChunkPositionComponent> pos, Entity entity) in
			         SystemAPI.Query<ChunkMeshData, RefRO<ChunkPositionComponent>>()
			                  .WithAll<IsVisible, HasMesh, NeedsColliderSync>()
			                  .WithEntityAccess())
			{
				var hasCollider  = SystemAPI.HasComponent<HasCollider>(entity);
				var needsRebuild = SystemAPI.HasComponent<NeedsColliderSync>(entity);

				if (hasCollider && !needsRebuild) continue;
				if (scheduledThisFrame >= 1) break;
				if (!IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;
				if (meshData.ChunkMesh == null || meshData.ChunkMesh.vertexCount == 0) continue;

				var alreadyPending = false;
				foreach (PendingBake b in pendingBakes)
					if (b.Entity == entity)
					{
						alreadyPending = true;
						break;
					}

				if (alreadyPending) continue;

				Mesh.MeshDataArray srcArray = Mesh.AcquireReadOnlyMeshData(meshData.ChunkMesh);
				var                collider = new NativeArray<BlobAssetReference<Collider>>(1, Allocator.Persistent);

				var job = new ColliderBakeJob
				          {
					          MeshDataArray = srcArray,
					          Collider      = collider,
					          Filter        = chunkFilter
				          };

				pendingBakes.Add(new PendingBake
				                 {
					                 Entity        = entity,
					                 Handle        = job.Schedule(),
					                 Collider      = collider,
					                 MeshDataArray = srcArray
				                 });

				JobHandle.ScheduleBatchedJobs();
				scheduledThisFrame++;
			}

			foreach ((RefRO<ChunkPositionComponent> pos, Entity entity) in
			         SystemAPI.Query<RefRO<ChunkPositionComponent>>()
			                  .WithAll<HasCollider>()
			                  .WithEntityAccess())
			{
				if (IsChebyshevNear(pos.ValueRO.ChunkCoord, playerChunk, COLLIDER_RADIUS)) continue;
				ecb.RemoveComponent<PhysicsCollider>(entity);
				ecb.RemoveComponent<PhysicsWorldIndex>(entity);
				ecb.RemoveComponent<HasCollider>(entity);
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
		}

		private static bool IsChebyshevNear(int3 a, int3 b, int radius)
		{
			int3 d = math.abs(a - b);
			return d.x <= radius && d.y <= radius && d.z <= radius;
		}

		private struct PendingBake
		{
			public Entity                                    Entity;
			public JobHandle                                 Handle;
			public NativeArray<BlobAssetReference<Collider>> Collider;
			public Mesh.MeshDataArray                        MeshDataArray;
		}
	}
}