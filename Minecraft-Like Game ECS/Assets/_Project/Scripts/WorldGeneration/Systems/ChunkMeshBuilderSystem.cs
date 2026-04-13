using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))][UpdateAfter(typeof(ChunkPopulateSystem))]
	[BurstCompile]
	public partial struct ChunkMeshBuilderSystem : ISystem
	{
		private NativeArray<int3> faceChecks;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<ChunkMapSingleton>();

			faceChecks = new NativeArray<int3>(6, Allocator.Persistent)
			             {
				             [0] = new int3(0, 0, -1),[1] = new int3(0, 0, 1),
				             [2] = new int3(0, 1, 0), [3]  = new int3(0, -1, 0),
				             [4] = new int3(-1, 0, 0),[5] = new int3(1, 0, 0)
			             };
		}

		public void OnDestroy(ref SystemState state)
		{
			faceChecks.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var query = SystemAPI.QueryBuilder()
			                     .WithAll<NeedsMeshSync, IsPopulated, ChunkPositionComponent, ChunkComponent>()
			                     .WithNone<MarkedToDestroy, IsEmpty>()
			                     .Build();
			if (query.IsEmpty) return;

			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);

			var allEntities  = query.ToEntityArray(Allocator.Temp);
			var allPositions = query.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			// Sort by distance
			var ordered = new NativeArray<MeshCandidate>(allEntities.Length, Allocator.Temp);
			for (int i = 0; i < allEntities.Length; i++)
			{
				int3 d = allPositions[i].ChunkCoord - playerChunk;
				ordered[i] = new MeshCandidate
				             {
					             Entity = allEntities[i],
					             Coord  = allPositions[i].ChunkCoord,
					             DistSq = d.x * d.x + d.y * d.y + d.z * d.z
				             };
			}

			ordered.Sort();

			allEntities.Dispose();
			allPositions.Dispose();

			var chunkMap        = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			var chunkLookup     = SystemAPI.GetSingleton<ChunkMapSingleton>();
			var populatedLookup = SystemAPI.GetComponentLookup<IsPopulated>(true);
			
			// FIX: Pre-allocate static length arrays to prevent NativeList job safety invalidation flaws.
			int maxTake         = math.min(ordered.Length, GameSettings.MAX_CONCURRENT_JOBS);
			var validEntities   = new NativeArray<Entity>(maxTake, Allocator.TempJob);
			var validPositions  = new NativeArray<int3>(maxTake, Allocator.TempJob);

			var ecbPre = new EntityCommandBuffer(Allocator.Temp);
			int count  = 0;

			for (int i = 0; i < ordered.Length; i++)
			{
				if (count >= maxTake) break;

				int3   pos    = ordered[i].Coord;
				Entity entity = ordered[i].Entity;

				bool ready = true;
				for (int f = 0; f < 6; f++)
				{
					int3 nPos = pos + faceChecks[f];
					if (chunkMap.TryGetValue(nPos, out Entity nEnt) &&
					    populatedLookup.HasComponent(nEnt) &&
					    chunkLookup.ChunkDataLookup.ContainsKey(nEnt) &&
					    chunkLookup.ChunkDataLookup[nEnt].BlockData.IsCreated) continue;
					ready = false;
					break;
				}

				if (!ready) continue;

				validEntities[count]  = entity;
				validPositions[count] = pos;
				count++;

				ecbPre.RemoveComponent<NeedsMeshSync>(entity);
				ecbPre.RemoveComponent<UrgentMeshSync>(entity);

				if (!SystemAPI.HasComponent<ChunkMeshData>(entity)) continue;
				SystemAPI.GetComponent<ChunkMeshData>(entity).Dispose();
				ecbPre.RemoveComponent<ChunkMeshData>(entity);
			}

			ordered.Dispose();
			ecbPre.Playback(state.EntityManager);
			ecbPre.Dispose();
			populatedLookup.Update(ref state);

			if (count > 0)
			{
				var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
				var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
				                   .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

				var job = new GreedyMeshJob
				          {
					          // Extract precise length safety-compatible subsets from oversized array 
					          Entities        = validEntities.GetSubArray(0, count),
					          Positions       = validPositions.GetSubArray(0, count),
					          ChunkMap        = chunkMap,
					          BlockDataLookup = chunkLookup.ChunkDataLookup,
					          BlockPrototypes = registry.Blocks,
					          CustomMeshes    = registry.Meshes,
					          FaceChecks      = faceChecks,
					          ECB             = ecb
				          };

				int batch = math.max(1, count / 16);
				state.Dependency = job.ScheduleParallelByRef(count, batch, state.Dependency);
			}

			// Clean up. Safely deferred against correct dependencies.
			validEntities.Dispose(state.Dependency);
			validPositions.Dispose(state.Dependency);
		}

		private struct MeshCandidate : System.IComparable<MeshCandidate>
		{
			public Entity Entity;
			public int3   Coord;
			public int    DistSq;
			public int    CompareTo(MeshCandidate other) => DistSq.CompareTo(other.DistSq);
		}
	}
}