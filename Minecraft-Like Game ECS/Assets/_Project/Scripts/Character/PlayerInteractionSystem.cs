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
{[UpdateInGroup(typeof(SimulationSystemGroup))][UpdateAfter(typeof(PlayerVisibleChunksSystem))][UpdateAfter(typeof(ChunkPopulateSystem))][UpdateBefore(typeof(ChunkMeshBuilderSystem))]
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
		private void ProcessInteraction(ref SystemState state, out EntityCommandBuffer ecb, ref WorldBlockRegistrySingleton registry, ref PlayerInteractionState playerInteractionState, bool breakPressed)
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

#if UNITY_EDITOR
			// Draw raw raycast
			Debug.DrawLine(rayStart, rayEnd, Color.yellow, 0.5f);
#endif

			if (!collisionWorld.CastRay(input, out RaycastHit hit)) return;

			float3 hitNormal = hit.SurfaceNormal;
			int3 snappedNormalInt = SnapNormal(ref hitNormal);

#if UNITY_EDITOR
			// Draw exact mathematical hit cross and normal
			Debug.DrawLine(hit.Position, hit.Position + (float3)snappedNormalInt * 0.5f, Color.red, 2f);
			Debug.DrawLine(hit.Position - new float3(0.05f), hit.Position + new float3(0.05f), Color.magenta, 2f);
			Debug.DrawLine(hit.Position - new float3(-0.05f, 0.05f, 0.05f), hit.Position + new float3(-0.05f, 0.05f, 0.05f), Color.magenta, 2f);
#endif

			// Step 1: Push slightly FORWARD along the view ray, and INWARD along the normal.
			// This completely isolates the single integer coordinate of the block we collided with,
			// eliminating all edge/corner floating point ambiguity.
			float3 insidePoint = hit.Position + viewLtw.Forward * 0.01f - new float3(snappedNormalInt) * 0.01f;
			
			int3 hitBlockCoord = new int3(
				(int)math.floor(insidePoint.x),
				(int)math.floor(insidePoint.y),
				(int)math.floor(insidePoint.z)
			);

			if (breakPressed)
			{
#if UNITY_EDITOR
				Color breakColor = Color.red;
				DrawDebugBox(ref hitBlockCoord, ref breakColor);
#endif

				var airBlockState = new BlockState { ID = 0 };
				float3 hitPosFloat = hitBlockCoord; // Cast integer exact coordinate to float for chunk mapping
				ModifyBlock(ref state, ref hitPosFloat, ref airBlockState, ecb);
			}
			else
			{
				// Step 2: Placing is just adding the integer normal to the hit block's integer coordinate.
				// This bypasses all floating point math completely.
				int3 placeBlockCoord = hitBlockCoord + snappedNormalInt;

#if UNITY_EDITOR
				Color placeColor = Color.green;
				DrawDebugBox(ref placeBlockCoord, ref placeColor);
#endif

				var blockAabb = new Aabb
				                {
					                Min = placeBlockCoord + new float3(0.05f),
					                Max = placeBlockCoord + new float3(0.95f)
				                };

				Aabb charAabb = charColliderComp.Value.Value.CalculateAabb(
				                                                           new RigidTransform(charTransform.Rotation,
				                                                            charTransform.Position));

				if (charAabb.Overlaps(blockAabb)) return;
				
				if (TryGetBlockAt(ref state, placeBlockCoord, out BlockState existingBlock))
				{
					bool isAir   = existingBlock.ID == 0;
					bool isFluid = existingBlock.ID != 0 && registry.Blocks[existingBlock.ID].IsFluid;
					if (!isAir && !isFluid) return;
				}
				
				Block blockProto  = registry.Blocks[playerInteractionState.SelectedBlockID];
				byte  orientation = 0;

				float3 normalFloat = new float3(snappedNormalInt);
				orientation = GetBlockOrientation(ref blockProto, ref viewLtw, ref normalFloat, orientation);

				var placedState = new BlockState
				                  {
					                  ID          = playerInteractionState.SelectedBlockID,
					                  Orientation = orientation
				                  };

				float3 placePosFloat = placeBlockCoord;
				ModifyBlock(ref state, ref placePosFloat, ref placedState, ecb);
			}
		}

#if UNITY_EDITOR
		private static void DrawDebugBox(ref int3 pos, ref Color color)
		{
			float3 min = pos;
			float3 max = pos + new int3(1, 1, 1);
			
			Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), color, 2f);
			Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), color, 2f);
			Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), color, 2f);
			Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), color, 2f);
			
			Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color, 2f);
			Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), color, 2f);
			Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), color, 2f);
			Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), color, 2f);
			
			Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), color, 2f);
			Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color, 2f);
			Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), color, 2f);
			Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), color, 2f);
		}
#endif

		private static int3 SnapNormal(ref float3 normal)
		{
			float3 absN = math.abs(normal);
			if (absN.x > absN.y && absN.x > absN.z) return new int3((int)math.sign(normal.x), 0, 0);
			if (absN.y > absN.x && absN.y > absN.z) return new int3(0, (int)math.sign(normal.y), 0);
			return new int3(0, 0, (int)math.sign(normal.z));
		}

		private static byte GetBlockOrientation(ref Block blockProto, ref LocalToWorld viewLtw, ref float3 normal, byte orientation)
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
					orientation = normal.y switch
					              {
						              > 0.5f => 0, < -0.5f => 1,
						              _ => normal.z switch
						                   {
							                   > 0.5f => 2, < -0.5f => 3,
							                   _      => normal.x switch { > 0.5f => 4, < -0.5f => 5, _ => orientation }
						                   }
					              };
					break;
				case BlockDirectionType.None:
				default: break;
			}

			return orientation;
		}
		
		private static void ScrollThroughBlocks(int maxBlockID, ref WorldBlockRegistrySingleton registry, ref PlayerInteractionState playerInteractionState)
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

		private void ModifyBlock(ref SystemState state, ref float3 worldPos, ref BlockState newBlock, EntityCommandBuffer ecb)
		{
			int3 chunkCoord = Utility.WorldToChunkCoord(worldPos);
			var  worldInt   = new int3(math.floor(worldPos));
			int3 localPos   = worldInt - chunkCoord * ChunkData.CHUNK_SIZE;

			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			
			if (!chunkMap.TryGetValue(chunkCoord, out Entity chunkEntity)) return;

			if (localPos.x < 0 || localPos.x >= ChunkData.CHUNK_SIZE ||
			    localPos.y < 0 || localPos.y >= ChunkData.CHUNK_SIZE ||
			    localPos.z < 0 || localPos.z >= ChunkData.CHUNK_SIZE)
				return;

			SafeCompleteNeighborChunks(ref state, ref chunkCoord);

			var chunkComp = SystemAPI.GetComponent<ChunkComponent>(chunkEntity);
			chunkComp.BlockData.SetAtIndex(localPos.x, localPos.y, localPos.z, newBlock);

			if (newBlock.ID != 0 && state.EntityManager.HasComponent<IsEmpty>(chunkEntity))
				ecb.RemoveComponent<IsEmpty>(chunkEntity);

			ecb.TryAddComponent<NeedsMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentColliderSync>(ref state, chunkEntity);

			TryMarkNeighbors(ref state, ecb, ref localPos, ref chunkCoord);
		}

		private void SafeCompleteNeighborChunks(ref SystemState state, ref int3 chunkCoord)
		{
			for (var x = -1; x <= 1; x++)
			for (var y = -1; y <= 1; y++)
			for (var z = -1; z <= 1; z++)
			{
				int3 nCoord = chunkCoord + new int3(x, y, z);
				SafeCompleteChunkJob(ref state, ref nCoord);
			}
		}
		
		private bool TryGetBlockAt(ref SystemState state, int3 worldCoord, out BlockState blockState)
		{
			blockState = default;
			float3 worldFloat = worldCoord;
			int3   chunkCoord = Utility.WorldToChunkCoord(worldFloat);
			int3   localPos   = worldCoord - chunkCoord * ChunkData.CHUNK_SIZE;

			if (!SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap
			              .TryGetValue(chunkCoord, out Entity chunkEntity)) return false;

			var chunkComp = SystemAPI.GetComponent<ChunkComponent>(chunkEntity);
			blockState = chunkComp.BlockData.GetAtPosition(localPos.x, localPos.y, localPos.z);
			return true;
		}

		private void SafeCompleteChunkJob(ref SystemState state, ref int3 coord)
		{
			if (!SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap.TryGetValue(coord, out Entity e)) return;
			if (state.EntityManager.HasComponent<ChunkActiveJob>(e))
				state.EntityManager.GetComponentData<ChunkActiveJob>(e).Handle.Complete();
		}

		private void TryMarkNeighbors(ref SystemState state, EntityCommandBuffer ecb, ref int3 localPos, ref int3 chunkCoord)
		{
			switch (localPos.x)
			{
				case 0:
				{
					int3 n = chunkCoord + new int3(-1, 0, 0); 
					TryMarkNeighbor(ref state, ref n, ecb);
					break;
				}
				case ChunkData.CHUNK_SIZE - 1:
				{
					int3 n = chunkCoord + new int3(1, 0, 0); 
					TryMarkNeighbor(ref state, ref n, ecb);
					break;
				}
			}

			switch (localPos.y)
			{
				case 0:
				{
					int3 n = chunkCoord + new int3(0, -1, 0); 
					TryMarkNeighbor(ref state, ref n, ecb);
					break;
				}
				case ChunkData.CHUNK_SIZE - 1:
				{
					int3 n = chunkCoord + new int3(0, 1, 0); 
					TryMarkNeighbor(ref state, ref n, ecb);
					break;
				}
			}

			switch (localPos.z)
			{
				case 0:
				{
					int3 n = chunkCoord + new int3(0, 0, -1); 
					TryMarkNeighbor(ref state, ref n, ecb);
					break;
				}
				case ChunkData.CHUNK_SIZE - 1:
				{
					int3 n = chunkCoord + new int3(0, 0, 1); 
					TryMarkNeighbor(ref state, ref n, ecb);
					break;
				}
			}
		}

		private void TryMarkNeighbor(ref SystemState state, ref int3 neighborCoord, EntityCommandBuffer ecb)
		{
			if (!SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap
			              .TryGetValue(neighborCoord, out Entity chunkEntity)) return;
			
			ecb.TryAddComponent<NeedsMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentMeshSync>(ref state, chunkEntity);
			ecb.TryAddComponent<UrgentColliderSync>(ref state, chunkEntity);
		}
	}
}