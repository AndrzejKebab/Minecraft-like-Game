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
			state.RequireForUpdate<PlayerInteractionState>();
			state.RequireForUpdate<FirstPersonPlayer>();

			var layer      = LayerMask.NameToLayer("Chunk");
			var chunkLayer = layer == -1 ? 1u << 3 : 1u << layer;
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
			ref PlayerInteractionState playerInteractionState = 
				ref SystemAPI.GetSingletonRW<PlayerInteractionState>().ValueRW;

			ScrollThroughBlocks(maxBlockID, ref registry, ref playerInteractionState);

			var breakPressed = playerInteractionState.BreakPressed;
			var placePressed = playerInteractionState.PlacePressed;

			if (!breakPressed && !placePressed) return;

			ProcessInteraction(ref state, out EntityCommandBuffer ecb, ref registry, ref playerInteractionState, breakPressed);
			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}
		
		[BurstCompile]
		private void ProcessInteraction(ref SystemState state, out EntityCommandBuffer ecb, ref WorldBlockRegistrySingleton registry, ref PlayerInteractionState  playerInteractionState, bool breakPressed)
		{
			ecb = new EntityCommandBuffer(Allocator.Temp);

			var    firstPersonPlayer = SystemAPI.GetSingleton<FirstPersonPlayer>();
			Entity charEntity        = firstPersonPlayer.ControlledCharacter;
			
			Entity viewEntity = SystemAPI.GetComponent<FirstPersonCharacterComponent>(charEntity).ViewEntity;

			var charTransform = SystemAPI.GetComponent<LocalTransform>(charEntity);
			var viewLtw       = SystemAPI.GetComponent<LocalToWorld>(viewEntity);

			var charColliderComp = SystemAPI.GetComponent<PhysicsCollider>(charEntity);

			float3         rayStart       = viewLtw.Position;
			float3         rayEnd         = viewLtw.Position + viewLtw.Forward * 6f;
			CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

			var input = new RaycastInput
			            {
				            Start  = rayStart,
				            End    = rayEnd,
				            Filter = raycastFilter
			            };

			if (!collisionWorld.CastRay(input, out RaycastHit hit)) return;

			if (breakPressed)
			{
				float3 blockPos   = hit.Position - hit.SurfaceNormal * 0.01f;
				var    airBlockState = new BlockState { ID = 0 };
				ModifyBlock(ref state, ref blockPos, ref airBlockState, ecb);
			}
			else
			{
				float3 blockPos = hit.Position + hit.SurfaceNormal * 0.01f;
				var worldInt = new int3(
				                        (int)math.floor(blockPos.x),
				                        (int)math.floor(blockPos.y),
				                        (int)math.floor(blockPos.z));

				var blockAabb = new Aabb
				                {
					                Min = worldInt + new float3(0.05f),
					                Max = worldInt + new float3(0.95f)
				                };

				Aabb charAabb = charColliderComp.Value.Value.CalculateAabb(
				                                                           new RigidTransform(charTransform.Rotation,
				                                                            charTransform.Position));

				var intersects = charAabb.Overlaps(blockAabb);

				if (intersects) return;

				Block blockProto  = registry.Blocks[playerInteractionState.SelectedBlockID];
				byte  orientation = 0;

				orientation = GetBlockOrientation(ref blockProto, ref viewLtw, ref hit, orientation);

				var placedState = new BlockState
				                  {
					                  ID          = playerInteractionState.SelectedBlockID,
					                  Orientation = orientation
				                  };

				ModifyBlock(ref state, ref  blockPos, ref placedState, ecb);
			}
		}

		[BurstCompile]
		private static byte GetBlockOrientation(ref Block      blockProto, ref LocalToWorld viewLtw,
		                                        ref RaycastHit hit,        byte             orientation)
		{
			switch (blockProto.DirectionType)
			{
				case BlockDirectionType.YAxis:
					float3 rawFwd = viewLtw.Forward;
					float3 hFwd =
						math.normalizesafe(new float3(rawFwd.x, 0f, rawFwd.z), new float3(0f, 0f, 1f));
					orientation = math.abs(hFwd.x) > math.abs(hFwd.z)
						              ? hFwd.x > 0 ? (byte)5 : (byte)4
						              : hFwd.z > 0
							              ? (byte)3
							              : (byte)2;
					break;
				case BlockDirectionType.AllAxes:
					float3 n = hit.SurfaceNormal;
					orientation = n.y switch
					              {
						              > 0.5f => 0, < -0.5f => 1,
						              _ => n.z switch
						                   {
							                   > 0.5f => 2, < -0.5f => 3,
							                   _      => n.x switch { > 0.5f => 4, < -0.5f => 5, _ => orientation }
						                   }
					              };
					break;
				case BlockDirectionType.None:
				default: break;
			}

			return orientation;
		}
		
		[BurstCompile]
		private static void ScrollThroughBlocks(int maxBlockID, ref WorldBlockRegistrySingleton registry,
		                                        ref PlayerInteractionState playerInteractionState)
		{
			if (maxBlockID < 1) return;
			var scroll = playerInteractionState.ScrollDelta;
			if (!(math.abs(scroll) > 0.01f)) return;
			var newID = playerInteractionState.SelectedBlockID;
			for (var i = 0; i < maxBlockID; i++)
			{
				switch (scroll)
				{
					case > 0:
					{
						newID++;
						if (newID > maxBlockID) newID = 1;
						break;
					}
					default:
					{
						newID--;
						if (newID < 1) newID = (ushort)maxBlockID;
						break;
					}
				}

				if (!registry.BlockNames[newID].IsEmpty) break;
			}

			playerInteractionState.SelectedBlockID = newID;
		}

		[BurstCompile]
		private void ModifyBlock(ref SystemState state, ref float3 worldPos, ref BlockState newBlock, EntityCommandBuffer ecb)
		{
			int3 chunkCoord = PlayerVisibleChunksSystem.WorldToChunkCoord(worldPos);
			var  worldInt   = new int3((int3)math.floor(worldPos));
			int3 localPos   = worldInt - chunkCoord * ChunkData.CHUNK_SIZE;

			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;

			if (!chunkMap.TryGetValue(chunkCoord, out Entity chunkEntity)) return;

			if (localPos.x < 0 || localPos.x >= ChunkData.CHUNK_SIZE ||
			    localPos.y < 0 || localPos.y >= ChunkData.CHUNK_SIZE ||
			    localPos.z < 0 || localPos.z >= ChunkData.CHUNK_SIZE)
				return;

			SafeCompleteNeighborChunks(ref state, chunkCoord);

			var chunkComp = SystemAPI.GetComponent<ChunkComponent>(chunkEntity);
			chunkComp.BlockData.SetAtIndex(localPos.x, localPos.y, localPos.z, newBlock);

			if (newBlock.ID != 0 && state.EntityManager.HasComponent<IsEmpty>(chunkEntity))
				ecb.RemoveComponent<IsEmpty>(chunkEntity);

			ecb.TryAddComponent<NeedsMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentColliderSync>(ref state, chunkEntity);

			TryMarkNeighbors(ref state, ecb, localPos, chunkCoord);
		}

		[BurstCompile]
		private void SafeCompleteNeighborChunks(ref SystemState state, int3 chunkCoord)
		{
			for (var x = -1; x <= 1; x++)
			for (var y = -1; y <= 1; y++)
			for (var z = -1; z <= 1; z++)
				SafeCompleteChunkJob(ref state, chunkCoord + new int3(x, y, z));
		}

		[BurstCompile]
		private void SafeCompleteChunkJob(ref SystemState state, int3 coord)
		{
			if (!SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap.TryGetValue(coord, out Entity e)) return;
			if (state.EntityManager.HasComponent<ChunkActiveJob>(e))
				state.EntityManager.GetComponentData<ChunkActiveJob>(e).Handle.Complete();
		}

		[BurstCompile]
		private void TryMarkNeighbors(ref SystemState state, EntityCommandBuffer ecb, int3 localPos, int3 chunkCoord)
		{
			switch (localPos.x)
			{
				case 0:                        TryMarkNeighbor(ref state, chunkCoord + new int3(-1, 0, 0), ecb); break;
				case ChunkData.CHUNK_SIZE - 1: TryMarkNeighbor(ref state, chunkCoord + new int3(1, 0, 0), ecb); break;
			}

			switch (localPos.y)
			{
				case 0:                        TryMarkNeighbor(ref state, chunkCoord + new int3(0, -1, 0), ecb); break;
				case ChunkData.CHUNK_SIZE - 1: TryMarkNeighbor(ref state, chunkCoord + new int3(0, 1, 0), ecb); break;
			}

			switch (localPos.z)
			{
				case 0:                        TryMarkNeighbor(ref state, chunkCoord + new int3(0, 0, -1), ecb); break;
				case ChunkData.CHUNK_SIZE - 1: TryMarkNeighbor(ref state, chunkCoord + new int3(0, 0, 1), ecb); break;
			}
		}

		[BurstCompile]
		private void TryMarkNeighbor(ref SystemState state, int3 neighborCoord, EntityCommandBuffer ecb)
		{
			if (!SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap
			              .TryGetValue(neighborCoord, out Entity chunkEntity)) return;

			
			ecb.TryAddComponent<NeedsMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentColliderSync>(ref state, chunkEntity);
		}
	}
}