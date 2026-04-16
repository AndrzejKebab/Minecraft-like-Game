using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine;

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
			var ecb = new EntityCommandBuffer(Allocator.Temp);

			RefRW<ChunkMapSingleton> mapSingleton = SystemAPI.GetSingletonRW<ChunkMapSingleton>();

			foreach ((RefRO<ChunkPositionComponent> _, Entity entity) in SystemAPI
			                                                             .Query<RefRO<ChunkPositionComponent>>()
			                                                             .WithAll<MarkedToDestroy>()
			                                                             .WithEntityAccess())
			{
				// Localized wait: Ensure no job is using this chunk or its neighbors before disposing memory structures
				int3 pos = SystemAPI.GetComponent<ChunkPositionComponent>(entity).ChunkCoord;

				if (SystemAPI.HasComponent<ChunkActiveJob>(entity))
					SystemAPI.GetComponent<ChunkActiveJob>(entity).Handle.Complete();

				NativeHashMap<int3, Entity> map = mapSingleton.ValueRO.ChunkMap;
				for (var x = -1; x <= 1; x++)
				for (var y = -1; y <= 1; y++)
				for (var z = -1; z <= 1; z++)
					if (map.TryGetValue(pos + new int3(x, y, z), out Entity neighbor))
						if (SystemAPI.HasComponent<ChunkActiveJob>(neighbor))
							SystemAPI.GetComponent<ChunkActiveJob>(neighbor).Handle.Complete();

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

				if (state.EntityManager.HasComponent<ChunkManagedMesh>(entity))
				{
					var managed = state.EntityManager.GetComponentObject<ChunkManagedMesh>(entity);

					// Unregister mesh from BRG batch.
					var egs = state.EntityManager.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
					if (egs != null && managed.MeshBatchID.value != 0)
						egs.UnregisterMesh(managed.MeshBatchID);

					// Destroy solid and fluid render companion entities.
					if (managed.SolidEntity != Entity.Null &&
					    state.EntityManager.Exists(managed.SolidEntity))
						ecb.DestroyEntity(managed.SolidEntity);
					if (managed.FluidEntity != Entity.Null &&
					    state.EntityManager.Exists(managed.FluidEntity))
						ecb.DestroyEntity(managed.FluidEntity);

					// Release the Mesh asset.
					if (managed.Mesh != null)
						Object.Destroy(managed.Mesh);
				}

				ecb.DestroyEntity(entity);
			}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}
	}
}