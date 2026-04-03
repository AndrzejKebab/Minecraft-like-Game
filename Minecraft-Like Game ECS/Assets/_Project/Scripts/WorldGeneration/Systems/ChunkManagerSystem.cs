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
			var popSystem = World.GetExistingSystemManaged<ChunkPopulateSystem>();
			var meshSystem = World.GetExistingSystemManaged<ChunkMeshBuilderSystem>();
			
			var ecb = new EntityCommandBuffer(Allocator.TempJob);

			foreach (var (_, entity) in SystemAPI.Query<RefRO<MarkedToDestroy>>().WithEntityAccess())
			{
				// Get the combined JobHandles for any background thread currently using this chunk
				JobHandle popHandle  = popSystem?.GetChunkDependency(entity) ?? default;
				JobHandle meshHandle = meshSystem?.GetChunkDependency(entity) ?? default;
				JobHandle combined   = JobHandle.CombineDependencies(popHandle, meshHandle);

				// If all jobs touching this chunk have naturally finished, it is safe to destroy
				if (!combined.IsCompleted) continue;
				combined.Complete(); // Satisfy the Unity Safety System

				// Safely dispose Block Data
				if (SystemAPI.HasComponent<ChunkComponent>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkComponent>(entity);
					if (comp.BlockData.IsCreated) comp.BlockData.Dispose();
				}

				// Safely dispose Mesh Data
				if (SystemAPI.HasComponent<ChunkMeshData>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkMeshData>(entity);
					comp.Dispose();
				}

				// Safely dispose Physics Colliders to prevent memory leaks
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