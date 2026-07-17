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
			// player crosses a boundary, but tearing it all down in one frame (a structural
			// change + BlockData/collider/mesh free per chunk) is a hitch. Drain it steadily.
			//
			// And never force-complete a job to tear its chunk down. A chunk is destroyed
			// only once its own job — and every neighbour job that might still be reading its
			// BlockData — has finished on its own (IsCompleted). Not-ready chunks stay marked
			// and are revisited next frame, so there's no main-thread stall waiting on a job.
			var budget    = GameSettings.ChunkDestroysPerFrame;
			var destroyed = 0;

			for (var index = 0; index < chunkEntities.Length && destroyed < budget; index++)
			{
				Entity entity = chunkEntities[index];
				int3   pos    = chunkPositionComponents[index].ChunkCoord;

				if (!ReadyToDestroy(ref state, mapSingleton, entity, pos)) continue;

				if (SystemAPI.TryGetComponent(entity, out ChunkActiveJob job))
					job.Handle.Complete(); // already IsCompleted — releases the fence, no stall

				TryCompleteNeighbors(ref state, mapSingleton, pos); // all IsCompleted — cheap

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
				destroyed++;
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

		/// <summary>
		///     True only when this chunk's own job and every neighbour job that could still
		///     be reading its BlockData (mesh/collider jobs sample the 3×3×3 neighbourhood)
		///     have finished on their own. Polls IsCompleted — never blocks — so a chunk whose
		///     jobs are still in flight is simply left marked and retried a later frame.
		/// </summary>
		private bool ReadyToDestroy(ref SystemState state, RefRW<ChunkMapSingleton> mapSingleton, Entity entity, int3 pos)
		{
			if (SystemAPI.TryGetComponent(entity, out ChunkActiveJob job) && !job.Handle.IsCompleted)
				return false;

			NativeHashMap<int3, Entity> map = mapSingleton.ValueRO.ChunkMap;
			for (var x = -1; x <= 1; x++)
			for (var y = -1; y <= 1; y++)
			for (var z = -1; z <= 1; z++)
				if (map.TryGetValue(pos + new int3(x, y, z), out Entity neighbor))
					if (SystemAPI.TryGetComponent(neighbor, out ChunkActiveJob neighborJob) &&
					    !neighborJob.Handle.IsCompleted)
						return false;

			return true;
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