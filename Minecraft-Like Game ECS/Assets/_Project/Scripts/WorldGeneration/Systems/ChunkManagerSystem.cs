using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;

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
			var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

			var mapSingleton = SystemAPI.GetSingletonRW<ChunkMapSingleton>();

			foreach (var (_, entity) in SystemAPI.Query<RefRO<ChunkPositionComponent>>().WithAll<MarkedToDestroy>()
			                                     .WithEntityAccess())
			{
				// Localized wait: Ensure no job is using this chunk or its neighbors before disposing memory structures
				int3 pos = SystemAPI.GetComponent<ChunkPositionComponent>(entity).ChunkCoord;

				if (SystemAPI.HasComponent<ChunkActiveJob>(entity))
					SystemAPI.GetComponent<ChunkActiveJob>(entity).Handle.Complete();

				var map = mapSingleton.ValueRO.ChunkMap;
				for (int x = -1; x <= 1; x++)
				{
					for (int y = -1; y <= 1; y++)
					{
						for (int z = -1; z <= 1; z++)
						{
							if (map.TryGetValue(pos + new int3(x, y, z), out Entity neighbor))
							{
								if (SystemAPI.HasComponent<ChunkActiveJob>(neighbor))
									SystemAPI.GetComponent<ChunkActiveJob>(neighbor).Handle.Complete();
							}
						}
					}
				}

				if (SystemAPI.HasComponent<ChunkComponent>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkComponent>(entity);
					if (comp.BlockData.IsCreated) comp.BlockData.Dispose();

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