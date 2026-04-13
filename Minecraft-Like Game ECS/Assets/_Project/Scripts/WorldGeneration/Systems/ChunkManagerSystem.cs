using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Entities;
using Unity.Physics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
	public partial struct ChunkManagerSystem : ISystem
	{
		public void OnCreate(ref SystemState state)
		{
		}

		public void OnDestroy(ref SystemState state)
		{
		}

		public void OnUpdate(ref SystemState state)
		{
			// Only sync jobs that actually touch ChunkComponent / ChunkMeshData / PhysicsCollider
			state.EntityManager.CompleteDependencyBeforeRW<ChunkComponent>();
			state.EntityManager.CompleteDependencyBeforeRW<ChunkMeshData>();
			// PhysicsCollider sync handled by physics group already

			var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

			// Access the singleton to remove entries from the manual lookup
			var mapSingleton = SystemAPI.GetSingletonRW<ChunkMapSingleton>();

			foreach (var (_, entity) in SystemAPI.Query<RefRO<ChunkPositionComponent>>().WithAll<MarkedToDestroy>()
			                                     .WithEntityAccess())
			{
				if (SystemAPI.HasComponent<ChunkComponent>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkComponent>(entity);
					if (comp.BlockData.IsCreated) comp.BlockData.Dispose();

					// Cleanup our manual lookup
					mapSingleton.ValueRW.ChunkDataLookup.Remove(entity);
				}

				if (SystemAPI.HasComponent<ChunkMeshData>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkMeshData>(entity);
					comp.Dispose();
				}

				if (SystemAPI.HasComponent<PhysicsCollider>(entity))
				{
					var phys = SystemAPI.GetComponent<PhysicsCollider>(entity);
					if (phys.Value.IsCreated) phys.Value.Dispose();
				}

				if (state.EntityManager.HasComponent<ChunkGfxBuffers>(entity))
				{
					var gfx = state.EntityManager.GetComponentObject<ChunkGfxBuffers>(entity);
					gfx.Dispose();
				}

				ecb.DestroyEntity(entity);
			}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}
	}
}