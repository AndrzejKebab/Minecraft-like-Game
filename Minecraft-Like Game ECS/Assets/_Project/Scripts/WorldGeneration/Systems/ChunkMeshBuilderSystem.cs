using System.Runtime.InteropServices;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(ChunkPopulateSystem))]
	public partial struct ChunkMeshBuilderSystem : ISystem
	{
		private NativeArray<float3> faceChecks;
		private NativeArray<float3> faceTangents;
		private NativeList<ActiveJob> activeJobs;
		
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<ChunkMapSingleton>();
			
			activeJobs = new NativeList<ActiveJob>(Allocator.Persistent);
			faceChecks =
				new NativeArray<float3>(6, Allocator.Persistent)
				{
					[0] = new float3(0, 0, -1),
					[1] = new float3(0, 0, 1),
					[2] = new float3(0, 1, 0),
					[3] = new float3(0, -1, 0),
					[4] = new float3(-1, 0, 0),
					[5] = new float3(1, 0, 0)
				};
			faceTangents =
				new NativeArray<float3>(6, Allocator.Persistent)
				{[0] = new float3(1, 0, 0),
					[1] = new float3(-1, 0, 0),
					[2] = new float3(1, 0, 0),
					[3] = new float3(-1, 0, 0),[4] = new float3(0, 0, -1),
					[5] = new float3(0, 0, 1)
				};
		}

		public void OnDestroy(ref SystemState state)
		{
			foreach (ActiveJob job in activeJobs)
			{
				job.Handle.Complete();
				job.SolidMesh.Dispose();
				job.FluidMesh.Dispose();
			}
			
			activeJobs.Dispose();
			faceChecks.Dispose();
			faceTangents.Dispose();
		}

		public void CancelJobFor(Entity entity)
		{
			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				if (activeJobs[i].Entity != entity) continue;
				ActiveJob job = activeJobs[i];
				job.Handle.Complete();
				job.SolidMesh.Dispose();
				job.FluidMesh.Dispose();
				activeJobs.RemoveAt(i);
			}
		}

		public JobHandle GetChunkDependency(Entity chunkEntity)
		{
			JobHandle result = default;
			foreach (ActiveJob job in activeJobs)
				if (job.Entity == chunkEntity || job.NBack == chunkEntity ||
				    job.NFront == chunkEntity || job.NTop == chunkEntity ||
				    job.NBottom == chunkEntity || job.NLeft == chunkEntity ||
				    job.NRight == chunkEntity)
					result = JobHandle.CombineDependencies(result, job.Handle);
			return result;
		}

		public void OnUpdate(ref SystemState state)
		{
			ProcessJobs(ref state);
			ProcessUrgentJobs(ref state);
			
			if (activeJobs.Count >= GameSettings.MAX_CONCURRENT_JOBS) return;
			
			var                             registry        = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			NativeHashMap<int3, Entity>     chunkMap        = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			ComponentLookup<IsPopulated>    populatedLookup = SystemAPI.GetComponentLookup<IsPopulated>(true);
			ComponentLookup<ChunkComponent> blockDataLookup = SystemAPI.GetComponentLookup<ChunkComponent>(true);

			var queue           = new NativePriorityQueue<Entity>(128, Allocator.Temp);
			var toStripMeshSync = new NativeList<Entity>(Allocator.Temp);

			foreach ((RefRO<ChunkComponent> _, RefRO<ChunkPositionComponent> posComp,
			          RefRO<ChunkPriorityComponent> priority, Entity entity) in
			         SystemAPI.Query<RefRO<ChunkComponent>,
				                  RefRO<ChunkPositionComponent>,
				                  RefRO<ChunkPriorityComponent>>()
			                  .WithAll<IsPopulated, NeedsMeshSync>()
			                  .WithNone<MarkedToDestroy, IsEmpty>()
			                  .WithEntityAccess())
			{
				var alreadyProcessing = false;
				foreach (ActiveJob j in activeJobs)
					if (j.Entity == entity)
					{
						alreadyProcessing = true;
						break;
					}

				if (alreadyProcessing) continue;

				int3 pos            = posComp.ValueRO.ChunkCoord;
				var  neighborsReady = true;
				for (var i = 0; i < 6; i++)
				{
					int3 nPos = pos + new int3(faceChecks[i]);
					if (chunkMap.TryGetValue(nPos, out Entity nEntity) &&
					    populatedLookup.HasComponent(nEntity) &&
					    blockDataLookup.HasComponent(nEntity) &&
					    blockDataLookup[nEntity].BlockData.IsCreated) continue;
					neighborsReady = false;
					break;
				}

				if (neighborsReady) queue.Enqueue(entity, priority.ValueRO.Distance);
			}

			if (!queue.IsEmpty)
			{
				var countToSchedule = math.min(GameSettings.MAX_CONCURRENT_JOBS - activeJobs.Count, queue.Count);

				for (var i = 0; i < countToSchedule; i++)
				{
					Entity entity = queue.Dequeue();
					int3   pos    = SystemAPI.GetComponent<ChunkPositionComponent>(entity).ChunkCoord;

					var    solidMesh = new NativeMesh(Allocator.Persistent);
					var    fluidMesh = new NativeMesh(Allocator.Persistent);
					Entity nZNeg     = chunkMap[pos + new int3(0, 0, -1)];
					Entity nZPos     = chunkMap[pos + new int3(0, 0, 1)];
					Entity nYNeg     = chunkMap[pos + new int3(0, -1, 0)];
					Entity nYPos     = chunkMap[pos + new int3(0, 1, 0)];
					Entity nXNeg     = chunkMap[pos + new int3(-1, 0, 0)];
					Entity nXPos     = chunkMap[pos + new int3(1, 0, 0)];

					var job = new GreedyMeshJob
					          {
						          Accessor = new ChunkAccessor 
						                     {
							                     Center       = blockDataLookup[entity].BlockData,
							                     NeighborZNeg = nZNeg != Entity.Null && blockDataLookup.HasComponent(nZNeg) && blockDataLookup[nZNeg].BlockData.IsCreated ? blockDataLookup[nZNeg].BlockData : default,
							                     NeighborZPos = nZPos != Entity.Null && blockDataLookup.HasComponent(nZPos) && blockDataLookup[nZPos].BlockData.IsCreated ? blockDataLookup[nZPos].BlockData : default,
							                     NeighborYNeg = nYNeg != Entity.Null && blockDataLookup.HasComponent(nYNeg) && blockDataLookup[nYNeg].BlockData.IsCreated ? blockDataLookup[nYNeg].BlockData : default,
							                     NeighborYPos = nYPos != Entity.Null && blockDataLookup.HasComponent(nYPos) && blockDataLookup[nYPos].BlockData.IsCreated ? blockDataLookup[nYPos].BlockData : default,
							                     NeighborXNeg = nXNeg != Entity.Null && blockDataLookup.HasComponent(nXNeg) && blockDataLookup[nXNeg].BlockData.IsCreated ? blockDataLookup[nXNeg].BlockData : default,
							                     NeighborXPos = nXPos != Entity.Null && blockDataLookup.HasComponent(nXPos) && blockDataLookup[nXPos].BlockData.IsCreated ? blockDataLookup[nXPos].BlockData : default,
							                     ChunkSize    = VoxelData.CHUNK_SIZE
						                     },
						          BlockPrototypes = registry.Blocks,
						          CustomMeshes    = registry.Meshes,
						          FaceChecks      = faceChecks,
						          FaceTangents    = faceTangents,
						          SolidMesh       = solidMesh,
						          FluidMesh       = fluidMesh
					          };

					job.RunByRef(); 

					activeJobs.Add(new ActiveJob
					               {
						               Entity    = entity,
						               Handle    = default, 
						               SolidMesh = solidMesh,
						               FluidMesh = fluidMesh
					               });
					
					toStripMeshSync.Add(entity);
				}

				foreach (Entity e in toStripMeshSync)
					state.EntityManager.RemoveComponent<NeedsMeshSync>(e);
				toStripMeshSync.Dispose();

				if (countToSchedule > 0) JobHandle.ScheduleBatchedJobs();
			}

			queue.Dispose();
		}

		private void ProcessJobs(ref SystemState state)
		{
			for (var i = activeJobs.Count - 1; i >= 0; i--)
			{
				ActiveJob job = activeJobs[i];
				if (!job.Handle.IsCompleted) continue;
				job.Handle.Complete();

				if (!state.EntityManager.Exists(job.Entity))
				{
					job.SolidMesh.Dispose();
					job.FluidMesh.Dispose();
					activeJobs.RemoveAt(i);
					continue;
				}

				var svCount = job.SolidMesh.Vertices.Length;
				var fvCount = job.FluidMesh.Vertices.Length;
				var siCount = job.SolidMesh.Triangles.Length;
				var fiCount = job.FluidMesh.Triangles.Length;
				var totalV  = svCount + fvCount;
				var totalI  = siCount + fiCount;

				if (totalV > 0)
				{
                    // Generate proper UnityEngine.Mesh for BRG
                    var meshDataArray = Mesh.AllocateWritableMeshData(1);
                    var meshData = meshDataArray[0];

                    var layout = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
                    layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
                    layout[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 1);
                    layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 1);
                    layout[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1);

                    meshData.SetVertexBufferParams(totalV, layout);
                    meshData.SetIndexBufferParams(totalI, IndexFormat.UInt32);

                    var verts = meshData.GetVertexData<Vertex>();
                    if (svCount > 0) NativeArray<Vertex>.Copy(job.SolidMesh.Vertices.AsArray(), 0, verts, 0, svCount);
                    if (fvCount > 0) NativeArray<Vertex>.Copy(job.FluidMesh.Vertices.AsArray(), 0, verts, svCount, fvCount);

                    var indices = meshData.GetIndexData<int>();
                    if (siCount > 0) NativeArray<int>.Copy(job.SolidMesh.Triangles.AsArray(), 0, indices, 0, siCount);
                    if (fiCount > 0)
                    {
                        var fluidSource = job.FluidMesh.Triangles.AsArray();
                        for (int k = 0; k < fiCount; k++) indices[siCount + k] = fluidSource[k] + svCount; // Offset fluid indices
                    }

                    meshData.subMeshCount = 2;
                    meshData.SetSubMesh(0, new SubMeshDescriptor(0, siCount), MeshUpdateFlags.DontRecalculateBounds);
                    meshData.SetSubMesh(1, new SubMeshDescriptor(siCount, fiCount), MeshUpdateFlags.DontRecalculateBounds);

                    var unityMesh = new Mesh();
                    int3 pos = SystemAPI.GetComponent<ChunkPositionComponent>(job.Entity).WorldPosition;
                    unityMesh.bounds = new Bounds(new Vector3(pos.x + 16f, pos.y + 16f, pos.z + 16f), new Vector3(32f, 32f, 32f));
                    
                    Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, unityMesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                    state.EntityManager.AddComponentData(job.Entity, new ChunkRendererData { 
                        Mesh = unityMesh, SolidIndexCount = siCount, FluidIndexCount = fiCount 
                    });
				}

				// --- MEMORY AND COLLIDER OPTIMIZATION SECTION ---
				job.FluidMesh.Dispose(); 
				
				SystemHandle colSystemHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<ChunkCollidersSystem>();
				ref ChunkCollidersSystem colSystem = ref state.WorldUnmanaged.GetUnsafeSystemRef<ChunkCollidersSystem>(colSystemHandle);
				colSystem.CancelPendingBakeFor(job.Entity);

				if (state.EntityManager.HasComponent<ChunkMeshData>(job.Entity)) {
					var old = state.EntityManager.GetComponentData<ChunkMeshData>(job.Entity);
					old.Dispose();
					state.EntityManager.RemoveComponent<ChunkMeshData>(job.Entity);
				}

				float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(SystemAPI.GetSingletonEntity<Player>()).ValueRO.Position;
				int3 playerChunk = PlayerVisibleChunksSystem.WorldToChunkCoord(playerPos);
				var needsCollider = math.abs(job.Entity.Index) >= 0 && 
				                    math.abs(SystemAPI.GetComponent<ChunkPositionComponent>(job.Entity).ChunkCoord.x - playerChunk.x) <= 1 && 
				                    math.abs(SystemAPI.GetComponent<ChunkPositionComponent>(job.Entity).ChunkCoord.y - playerChunk.y) <= 1 && 
				                    math.abs(SystemAPI.GetComponent<ChunkPositionComponent>(job.Entity).ChunkCoord.z - playerChunk.z) <= 1;

				if (needsCollider && svCount > 0) 
				{
					job.SolidMesh.Vertices.TrimExcess(); 
					job.SolidMesh.Triangles.TrimExcess();

					state.EntityManager.AddComponentData(job.Entity, new ChunkMeshData { SolidMesh = job.SolidMesh, FluidMesh = default });
    
					if (!state.EntityManager.HasComponent<NeedsColliderSync>(job.Entity))
						state.EntityManager.AddComponent<NeedsColliderSync>(job.Entity);
				} 
				else 
				{
					job.SolidMesh.Dispose();
					state.EntityManager.RemoveComponent<NeedsColliderSync>(job.Entity);
				}

				if (!state.EntityManager.HasComponent<HasMesh>(job.Entity)) state.EntityManager.AddComponent<HasMesh>(job.Entity);
				activeJobs.RemoveAt(i);
			}
		}
		
		private void ProcessUrgentJobs(ref SystemState state)
        {
            EntityQuery urgentQuery = SystemAPI.QueryBuilder()
                                             .WithAll<UrgentMeshSync, IsPopulated, ChunkPositionComponent>()
                                             .WithNone<MarkedToDestroy, IsEmpty>()
                                             .Build();

            if (urgentQuery.IsEmpty) return;

            var                             registry        = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
            NativeHashMap<int3, Entity>     chunkMap        = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
            ComponentLookup<ChunkComponent> blockDataLookup = SystemAPI.GetComponentLookup<ChunkComponent>(true);

            using NativeArray<Entity> urgentChunks = urgentQuery.ToEntityArray(Allocator.Temp);
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (Entity entity in urgentChunks)
            {
                CancelJobFor(entity);

                int3 pos = SystemAPI.GetComponent<ChunkPositionComponent>(entity).ChunkCoord;
                
                var    solidMesh = new NativeMesh(Allocator.Persistent);
                var    fluidMesh = new NativeMesh(Allocator.Persistent);
                Entity nZNeg     = chunkMap[pos + new int3(0, 0, -1)];
                Entity nZPos     = chunkMap[pos + new int3(0, 0, 1)];
                Entity nYNeg     = chunkMap[pos + new int3(0, -1, 0)];
                Entity nYPos     = chunkMap[pos + new int3(0, 1, 0)];
                Entity nXNeg     = chunkMap[pos + new int3(-1, 0, 0)];
                Entity nXPos     = chunkMap[pos + new int3(1, 0, 0)];

                var job = new GreedyMeshJob
                          {
	                          Accessor = new ChunkAccessor 
	                                     {
		                                     Center       = blockDataLookup[entity].BlockData,
		                                     NeighborZNeg = nZNeg != Entity.Null && blockDataLookup.HasComponent(nZNeg) && blockDataLookup[nZNeg].BlockData.IsCreated ? blockDataLookup[nZNeg].BlockData : default,
		                                     NeighborZPos = nZPos != Entity.Null && blockDataLookup.HasComponent(nZPos) && blockDataLookup[nZPos].BlockData.IsCreated ? blockDataLookup[nZPos].BlockData : default,
		                                     NeighborYNeg = nYNeg != Entity.Null && blockDataLookup.HasComponent(nYNeg) && blockDataLookup[nYNeg].BlockData.IsCreated ? blockDataLookup[nYNeg].BlockData : default,
		                                     NeighborYPos = nYPos != Entity.Null && blockDataLookup.HasComponent(nYPos) && blockDataLookup[nYPos].BlockData.IsCreated ? blockDataLookup[nYPos].BlockData : default,
		                                     NeighborXNeg = nXNeg != Entity.Null && blockDataLookup.HasComponent(nXNeg) && blockDataLookup[nXNeg].BlockData.IsCreated ? blockDataLookup[nXNeg].BlockData : default,
		                                     NeighborXPos = nXPos != Entity.Null && blockDataLookup.HasComponent(nXPos) && blockDataLookup[nXPos].BlockData.IsCreated ? blockDataLookup[nXPos].BlockData : default,
		                                     ChunkSize    = VoxelData.CHUNK_SIZE
	                                     },
	                          BlockPrototypes = registry.Blocks,
	                          CustomMeshes    = registry.Meshes,
	                          FaceChecks      = faceChecks,
	                          FaceTangents    = faceTangents,
	                          SolidMesh       = solidMesh,
	                          FluidMesh       = fluidMesh
                          };

                job.RunByRef(); 

                activeJobs.Add(new ActiveJob
                {
                    Entity    = entity,
                    Handle    = default, 
                    SolidMesh = solidMesh,
                    FluidMesh = fluidMesh
                });

                ecb.RemoveComponent<UrgentMeshSync>(entity);
                ecb.RemoveComponent<NeedsMeshSync>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            ProcessJobs(ref state); 
        }

		private struct ActiveJob
		{
			public Entity     Entity;
			public Entity     NBack, NFront, NTop, NBottom, NLeft, NRight;
			public JobHandle  Handle;
			public NativeMesh SolidMesh;
			public NativeMesh FluidMesh;
		}
	}
}