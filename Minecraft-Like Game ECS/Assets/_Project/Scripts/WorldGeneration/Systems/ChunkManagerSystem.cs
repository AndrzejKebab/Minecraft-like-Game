using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
	public partial class ChunkManagerSystem : SystemBase
	{
		protected override void OnUpdate()
		{
			var popSystem  = World.GetExistingSystemManaged<ChunkPopulateSystem>();
			var meshSystem = World.GetExistingSystemManaged<ChunkMeshBuilderSystem>();

			var ecb = new EntityCommandBuffer(Allocator.TempJob);

			foreach ((_, Entity entity) in SystemAPI.Query<RefRO<MarkedToDestroy>>().WithEntityAccess())
			{
				JobHandle popHandle  = popSystem?.GetChunkDependency(entity) ?? default;
				JobHandle meshHandle = meshSystem?.GetChunkDependency(entity) ?? default;
				JobHandle combined   = JobHandle.CombineDependencies(popHandle, meshHandle);

				if (!combined.IsCompleted) continue;
				combined.Complete();

				if (SystemAPI.HasComponent<ChunkComponent>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkComponent>(entity);
					if (comp.BlockData.IsCreated) comp.BlockData.Dispose();
				}

				if (EntityManager.HasComponent<ChunkMeshData>(entity))
				{
					var comp = EntityManager.GetComponentData<ChunkMeshData>(entity);
					comp.Dispose();
				}

				if (SystemAPI.HasComponent<PhysicsCollider>(entity))
				{
					var phys = SystemAPI.GetComponent<PhysicsCollider>(entity);
					if (phys.Value.IsCreated) phys.Value.Dispose();
				}

				ecb.DestroyEntity(entity);
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
		}
	}
}