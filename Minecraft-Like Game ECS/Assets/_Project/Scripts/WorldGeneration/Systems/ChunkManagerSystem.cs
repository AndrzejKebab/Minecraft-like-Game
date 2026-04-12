using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;

namespace _Project.WorldGeneration.Systems
{[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
	public partial struct ChunkManagerSystem : ISystem
	{
		public void OnUpdate(ref SystemState state)
		{
			SystemHandle          popSystemHandle       = state.WorldUnmanaged.GetExistingUnmanagedSystem<ChunkPopulateSystem>();
			ref ChunkPopulateSystem popSystem				   = ref state.WorldUnmanaged.GetUnsafeSystemRef<ChunkPopulateSystem>(popSystemHandle);
			SystemHandle          meshSystemHandle      = state.WorldUnmanaged.GetExistingUnmanagedSystem<ChunkMeshBuilderSystem>();
			ref ChunkMeshBuilderSystem meshSystem				   = ref state.WorldUnmanaged.GetUnsafeSystemRef<ChunkMeshBuilderSystem>(meshSystemHandle);
			SystemHandle colSystemHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<ChunkCollidersSystem>();
			ref ChunkCollidersSystem      colSystem       = ref state.WorldUnmanaged.GetUnsafeSystemRef<ChunkCollidersSystem>(colSystemHandle);
			var          ecb             = new EntityCommandBuffer(Allocator.Temp);

			var oldCollidersToDispose = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);
			var oldBlocksToDispose    = new NativeList<NativeArray<BlockState>>(Allocator.Temp);

			foreach ((_, Entity entity) in SystemAPI.Query<RefRO<MarkedToDestroy>>().WithEntityAccess())
			{
				JobHandle popHandle  = popSystem.GetChunkDependency(entity);
				JobHandle meshHandle = meshSystem.GetChunkDependency(entity);
				JobHandle combined   = JobHandle.CombineDependencies(popHandle, meshHandle);

				if (!combined.IsCompleted) continue;
				combined.Complete();

				colSystem.CancelPendingBakeFor(entity); 

				if (SystemAPI.HasComponent<ChunkComponent>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkComponent>(entity);
					if (comp.BlockData.IsCreated) oldBlocksToDispose.Add(comp.BlockData);
				}

				if (SystemAPI.HasComponent<ChunkMeshData>(entity))
				{
					var comp = SystemAPI.GetComponent<ChunkMeshData>(entity);
					comp.Dispose();
				}

				if (SystemAPI.HasComponent<PhysicsCollider>(entity))
				{
					var phys = SystemAPI.GetComponent<PhysicsCollider>(entity);
					if (phys.Value.IsCreated) oldCollidersToDispose.Add(phys.Value);
				}
				
				if (SystemAPI.HasComponent<VoxelMeshAllocation>(entity))
				{
					var alloc = SystemAPI.GetComponent<VoxelMeshAllocation>(entity);
					if (alloc.IsAllocated)
					{
						var megaBufferSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<MegaBufferSystem>();
						megaBufferSystem.Free(alloc.VertexOffset, alloc.VertexCount, alloc.IndexOffset, alloc.SolidIndexCount + alloc.FluidIndexCount);
					}
				}

				ecb.DestroyEntity(entity);
			}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();

			foreach (BlobAssetReference<Collider> c in oldCollidersToDispose) c.Dispose();
			oldCollidersToDispose.Dispose();

			foreach (NativeArray<BlockState> b in oldBlocksToDispose) b.Dispose();
			oldBlocksToDispose.Dispose();
		}
	}
}