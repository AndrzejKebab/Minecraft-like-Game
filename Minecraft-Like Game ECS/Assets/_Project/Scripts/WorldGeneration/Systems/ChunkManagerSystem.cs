using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Burst;
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
		private EntityQuery chunksToDestroy;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			chunksToDestroy = SystemAPI.QueryBuilder().WithAll<ChunkPositionComponent, MarkedToDestroy>().Build();
		}

		public void OnDestroy(ref SystemState state)
		{
		}

		public void OnUpdate(ref SystemState state)
		{
			var ecb = new EntityCommandBuffer(Allocator.Temp);

			RefRW<ChunkMapSingleton> mapSingleton = SystemAPI.GetSingletonRW<ChunkMapSingleton>();

			NativeArray<Entity> chunkEntities = chunksToDestroy.ToEntityArray(Allocator.Temp);
			NativeArray<ChunkPositionComponent> chunkPositionComponents =
				chunksToDestroy.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			// Budget destruction per frame — the whole shell is marked at once when the
			// player crosses a boundary, but tearing it all down in one frame (a .Complete()
			// stall + BlockData/collider/mesh free per chunk) is a hitch. Drain it steadily.
			var toDestroy = math.min(chunkEntities.Length, GameSettings.CHUNK_DESTROYS_PER_FRAME);

			for (var index = 0; index < toDestroy; index++)
			{
				Entity entity = chunkEntities[index];
				int3   pos    = chunkPositionComponents[index].ChunkCoord;

				if (SystemAPI.TryGetComponent(entity, out ChunkActiveJob job))
					job.Handle.Complete();

				TryCompleteNeighbors(ref state, mapSingleton, pos);

				if (SystemAPI.TryGetComponent(entity, out ChunkComponent chunk))
				{
					if (chunk.BlockData.IsCreated) chunk.BlockData.Dispose();
					mapSingleton.ValueRW.ChunkDataLookup.Remove(entity);
				}

				if (SystemAPI.TryGetComponent(entity, out ChunkMeshData data)) data.Dispose();

				if (SystemAPI.TryGetComponent(entity, out PhysicsCollider collider))
					if (collider.Value.IsCreated)
						collider.Value.Dispose();


				DestroyMesh(ref state, entity, ecb);

				// remove from the loaded map here (marking no longer does) so a chunk stays
				// revivable until this point; after this it's gone and re-entry recreates it
				mapSingleton.ValueRW.ChunkMap.Remove(pos);

				ecb.DestroyEntity(entity);
			}

			chunkEntities.Dispose();
			chunkPositionComponents.Dispose();

			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}

		private static void DestroyMesh(ref SystemState state, Entity entity, EntityCommandBuffer ecb)
		{
			if (!state.EntityManager.HasComponent<ChunkManagedMesh>(entity)) return;
			var mesh = state.EntityManager.GetComponentData<ChunkManagedMesh>(entity);
			// Unregister mesh from BRG batch.
			var egs = state.EntityManager.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
			if (egs != null && mesh.MeshBatchID.value != 0)
				egs.UnregisterMesh(mesh.MeshBatchID);

			// Destroy solid and fluid render companion entities.
			if (mesh.SolidEntity != Entity.Null &&
			    state.EntityManager.Exists(mesh.SolidEntity))
				ecb.DestroyEntity(mesh.SolidEntity);
			if (mesh.FluidEntity != Entity.Null &&
			    state.EntityManager.Exists(mesh.FluidEntity))
				ecb.DestroyEntity(mesh.FluidEntity);

			// Release the Mesh asset.
			if (mesh.Mesh.Value != null)
				Object.Destroy(mesh.Mesh.Value);
		}

		private void TryCompleteNeighbors(ref SystemState state, RefRW<ChunkMapSingleton> mapSingleton, int3 pos)
		{
			NativeHashMap<int3, Entity> map = mapSingleton.ValueRO.ChunkMap;
			for (var x = -1; x <= 1; x++)
			for (var y = -1; y <= 1; y++)
			for (var z = -1; z <= 1; z++)
				if (map.TryGetValue(pos + new int3(x, y, z), out Entity neighbor))
					if (SystemAPI.TryGetComponent(neighbor, out ChunkActiveJob neighborJob))
						neighborJob.Handle.Complete();
		}
	}
}