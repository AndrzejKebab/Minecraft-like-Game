using System;
using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace _Project.Character
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(PlayerVisibleChunksSystem))]
	[UpdateAfter(typeof(ChunkPopulateSystem))]
	[UpdateBefore(typeof(ChunkMeshBuilderSystem))]
	[BurstCompile]
	public partial struct PlayerInteractionSystem : ISystem
	{
		private CollisionFilter raycastFilter;
		
		[BurstDiscard]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PhysicsWorldSingleton>();
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<ChunkMapSingleton>();

			var chunkLayer = (uint)(1 << LayerMask.NameToLayer("Chunk"));
			raycastFilter = new CollisionFilter
			                {
				                BelongsTo    = ~0u,
				                CollidesWith = chunkLayer
			                };
		}
		
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var registry   = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			var maxBlockID = registry.Blocks.Length - 1;
			var ecb        = new EntityCommandBuffer(Allocator.Temp);

			foreach ((RefRW<PlayerInteractionState> interactState, RefRO<FirstPersonPlayer> player) in SystemAPI
				         .Query<RefRW<PlayerInteractionState>, RefRO<FirstPersonPlayer>>())
			{
				if (maxBlockID >= 1)
				{
					var scroll = interactState.ValueRO.ScrollDelta;
					if (math.abs(scroll) > 0.01f)
					{
						var newID = interactState.ValueRO.SelectedBlockID;

						for (var i = 0; i < maxBlockID; i++)
						{
							if (scroll > 0)
							{
								newID++;
								if (newID > maxBlockID) newID = 1;
							}
							else
							{
								newID--;
								if (newID < 1) newID = (ushort)maxBlockID;
							}

							if (!registry.BlockNames[newID].IsEmpty) break;
						}

						interactState.ValueRW.SelectedBlockID = newID;
					}
				}

				if (interactState.ValueRO is { BreakPressed: false, PlacePressed: false }) continue;

				if (!SystemAPI.HasComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter)) continue;
				if (!SystemAPI.HasComponent<PhysicsCollider>(player.ValueRO.ControlledCharacter)) continue;

				Entity viewEntity = SystemAPI.GetComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter).ViewEntity;
				if (!SystemAPI.HasComponent<LocalToWorld>(viewEntity)) continue;

				var charTransform = SystemAPI.GetComponent<LocalTransform>(player.ValueRO.ControlledCharacter);
				var charCollider  = SystemAPI.GetComponent<PhysicsCollider>(player.ValueRO.ControlledCharacter);
				var viewLtw = SystemAPI.GetComponent<LocalToWorld>(viewEntity);
				CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

				var input = new RaycastInput()
				            {
					            Start  = viewLtw.Position,
					            End    = viewLtw.Position + viewLtw.Forward * 6f, // 6 block reach
					            Filter = raycastFilter
				            };

				if (!collisionWorld.CastRay(input, out RaycastHit hit)) continue;
				if (interactState.ValueRO.BreakPressed)
				{
					float3 blockPos = hit.Position - hit.SurfaceNormal * 0.01f;
					ModifyBlock(ref state, blockPos, new BlockState() { ID = 0 }, ecb);
				}
				else if (interactState.ValueRO.PlacePressed)
				{
					float3 blockPos = hit.Position + hit.SurfaceNormal * 0.01f;
					var worldInt = new int3((int)math.floor(blockPos.x), (int)math.floor(blockPos.y), (int)math.floor(blockPos.z));
					var blockAabb = new Aabb { Min = worldInt + new float3(0.05f), Max = worldInt + new float3(0.95f) };

					Aabb charAabb = charCollider.Value.Value.CalculateAabb(new RigidTransform(charTransform.Rotation, charTransform.Position));

					var intersectsPlayer = blockAabb.Max.x > charAabb.Min.x && blockAabb.Min.x < charAabb.Max.x &&
					                       blockAabb.Max.y > charAabb.Min.y && blockAabb.Min.y < charAabb.Max.y &&
					                       blockAabb.Max.z > charAabb.Min.z && blockAabb.Min.z < charAabb.Max.z;

					if (intersectsPlayer) continue;
					Block blockProto  = registry.Blocks[interactState.ValueRO.SelectedBlockID];
					byte  orientation = 0;

					switch (blockProto.DirectionType)
					{
						case BlockDirectionType.YAxis:
							float3 forward = viewLtw.Forward;
							if (math.abs(forward.x) > math.abs(forward.z)) orientation = forward.x > 0 ? (byte)5 : (byte)4;
							else orientation = forward.z > 0 ? (byte)3 : (byte)2;
							break;
						case BlockDirectionType.AllAxes:
							float3 n = hit.SurfaceNormal;
							orientation = n.y switch { > 0.5f => 0, < -0.5f => 1, _ => n.z switch { > 0.5f => 2, < -0.5f => 3, _ => n.x switch { > 0.5f => 4, < -0.5f => 5, _ => orientation } } };
							break;
						case BlockDirectionType.None: break;
						default: throw new ArgumentOutOfRangeException();
					}

					var placedState = new BlockState { ID = interactState.ValueRO.SelectedBlockID, Orientation = orientation };
					ModifyBlock(ref state, blockPos, placedState, ecb);
				}
			}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}
		
		[BurstCompile]
		private void ModifyBlock(ref SystemState state, float3 worldPos, BlockState newBlock, EntityCommandBuffer ecb)
		{
			int3 chunkCoord = PlayerVisibleChunksSystem.WorldToChunkCoord(worldPos);
			var worldInt = new int3((int)math.floor(worldPos.x), (int)math.floor(worldPos.y), (int)math.floor(worldPos.z));
			int3 localPos = worldInt - chunkCoord * VoxelData.CHUNK_SIZE;

			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			if (!chunkMap.TryGetValue(chunkCoord, out Entity chunkEntity)) return;

			if (localPos.x < 0 || localPos.x >= VoxelData.CHUNK_SIZE ||
			    localPos.y < 0 || localPos.y >= VoxelData.CHUNK_SIZE ||
			    localPos.z < 0 || localPos.z >= VoxelData.CHUNK_SIZE) return;

            // This single line tells ECS to freeze all jobs globally so you can securely write to the chunk
            state.Dependency.Complete(); 

			var chunkComp = SystemAPI.GetComponent<ChunkComponent>(chunkEntity);
			chunkComp.BlockData.SetAtIndex(localPos.x, localPos.y, localPos.z, newBlock);

			if (newBlock.ID != 0 && state.EntityManager.HasComponent<IsEmpty>(chunkEntity))
				ecb.RemoveComponent<IsEmpty>(chunkEntity);

			if (!state.EntityManager.HasComponent<NeedsMeshSync>(chunkEntity))
				ecb.AddComponent<NeedsMeshSync>(chunkEntity);

			switch (localPos.x)
			{
				case 0:                        TryMarkNeighbor(ref state, chunkCoord + new int3(-1, 0, 0), ecb); break;
				case VoxelData.CHUNK_SIZE - 1: TryMarkNeighbor(ref state, chunkCoord + new int3(1, 0, 0), ecb); break;
			}
			switch (localPos.y)
			{
				case 0:                        TryMarkNeighbor(ref state, chunkCoord + new int3(0, -1, 0), ecb); break;
				case VoxelData.CHUNK_SIZE - 1: TryMarkNeighbor(ref state, chunkCoord + new int3(0, 1, 0), ecb); break;
			}
			switch (localPos.z)
			{
				case 0:                        TryMarkNeighbor(ref state, chunkCoord + new int3(0, 0, -1), ecb); break;
				case VoxelData.CHUNK_SIZE - 1: TryMarkNeighbor(ref state, chunkCoord + new int3(0, 0, 1), ecb); break;
			}
		}
		
		[BurstCompile]
		private void TryMarkNeighbor(ref SystemState state, int3 neighborCoord, EntityCommandBuffer ecb)
		{
			if (!SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap.TryGetValue(neighborCoord, out Entity chunkEntity)) return;

			if (!state.EntityManager.HasComponent<NeedsMeshSync>(chunkEntity))
				ecb.AddComponent<NeedsMeshSync>(chunkEntity);
		}
	}
}