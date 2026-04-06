using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace _Project.Character
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	public partial class PlayerInteractionSystem : SystemBase
	{
		private static readonly uint chunkLayer = (uint)(1 << UnityEngine.LayerMask.NameToLayer("Chunk"));

		private static readonly CollisionFilter raycastFilter = new()
		                                                        {
			                                                        BelongsTo    = ~0u,
			                                                        CollidesWith = chunkLayer
		                                                        };

		protected override void OnCreate()
		{
			RequireForUpdate<PhysicsWorldSingleton>();
			RequireForUpdate<WorldBlockRegistrySingleton>();
			RequireForUpdate<ChunkMapSingleton>();
		}

		protected override void OnUpdate()
		{
			var registry   = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			var maxBlockID = registry.Blocks.Length - 1;

			var popSystem  = World.GetExistingSystemManaged<ChunkPopulateSystem>();
			var meshSystem = World.GetExistingSystemManaged<ChunkMeshBuilderSystem>();
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

							if (!registry.Blocks[newID].Name.IsEmpty)
							{
								break;
							}
						}

						interactState.ValueRW.SelectedBlockID = newID;
					}
				}

				if (interactState.ValueRO is { BreakPressed: false, PlacePressed: false }) continue;

				if (!SystemAPI.HasComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter))
					continue;
				if (!SystemAPI.HasComponent<PhysicsCollider>(player.ValueRO.ControlledCharacter)) continue;

				Entity viewEntity = SystemAPI
				                    .GetComponent<FirstPersonCharacterComponent>(player.ValueRO.ControlledCharacter)
				                    .ViewEntity;
				if (!SystemAPI.HasComponent<LocalToWorld>(viewEntity)) continue;

				var charTransform = SystemAPI.GetComponent<LocalTransform>(player.ValueRO.ControlledCharacter);
				var charCollider  = SystemAPI.GetComponent<PhysicsCollider>(player.ValueRO.ControlledCharacter);

				var viewLtw = SystemAPI.GetComponent<LocalToWorld>(viewEntity);
				CollisionWorld collisionWorld =
					SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

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
					ModifyBlock(blockPos, new BlockState(){ID = 0}, ecb, popSystem, meshSystem);
				}
				else if (interactState.ValueRO.PlacePressed)
				{
					float3 blockPos = hit.Position + hit.SurfaceNormal * 0.01f;
					var worldInt = new int3((int)math.floor(blockPos.x), (int)math.floor(blockPos.y),
					                        (int)math.floor(blockPos.z));

					var blockAabb = new Aabb
					                {
						                Min = worldInt + new float3(0.05f),
						                Max = worldInt + new float3(0.95f)
					                };

					Aabb charAabb =
						charCollider.Value.Value.CalculateAabb(new RigidTransform(charTransform.Rotation,
						                                        charTransform.Position));

					var intersectsPlayer = blockAabb.Max.x > charAabb.Min.x && blockAabb.Min.x < charAabb.Max.x &&
					                       blockAabb.Max.y > charAabb.Min.y && blockAabb.Min.y < charAabb.Max.y &&
					                       blockAabb.Max.z > charAabb.Min.z && blockAabb.Min.z < charAabb.Max.z;

					if (!intersectsPlayer)
					{
						Block blockProto  = registry.Blocks[interactState.ValueRO.SelectedBlockID];
						byte  orientation = 0;

						if (blockProto.DirectionType == BlockDirectionType.YAxis)
						{
							// Furnaces face the player horizontally based on look direction
							float3 forward = viewLtw.Forward;
							if (math.abs(forward.x) > math.abs(forward.z))
								orientation = forward.x > 0 ? (byte)5 : (byte)4; // East look -> face West (X-)
							else
								orientation = forward.z > 0 ? (byte)3 : (byte)2; // North look -> face South (Z-)
						}
						else if (blockProto.DirectionType == BlockDirectionType.AllAxes)
						{
							// Logs align their "Top" to the normal of the surface they are attached to
							float3 n = hit.SurfaceNormal;
							if (n.y > 0.5f) orientation       = 0;
							else if (n.y < -0.5f) orientation = 1;
							else if (n.z > 0.5f) orientation  = 2;
							else if (n.z < -0.5f) orientation = 3;
							else if (n.x > 0.5f) orientation  = 4;
							else if (n.x < -0.5f) orientation = 5;
						}

						BlockState placedState = new BlockState { ID = interactState.ValueRO.SelectedBlockID, Orientation = orientation };
						ModifyBlock(blockPos, placedState, ecb, popSystem, meshSystem);
					}
				}
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
		}

		private void ModifyBlock(float3              worldPos,  BlockState newBlockID, EntityCommandBuffer ecb,
		                         ChunkPopulateSystem popSystem, ChunkMeshBuilderSystem meshSystem)
		{
			int3 chunkCoord = PlayerVisibleChunksSystem.WorldToChunkCoord(worldPos);
			var worldInt = new int3((int)math.floor(worldPos.x), (int)math.floor(worldPos.y),
			                        (int)math.floor(worldPos.z));
			int3 localPos = worldInt - chunkCoord * VoxelData.CHUNK_SIZE;

			NativeHashMap<int3, Entity> chunkMap = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			if (!chunkMap.TryGetValue(chunkCoord, out Entity chunkEntity)) return;

			JobHandle popHandle  = popSystem?.GetChunkDependency(chunkEntity) ?? default;
			JobHandle meshHandle = meshSystem?.GetChunkDependency(chunkEntity) ?? default;
			JobHandle.CombineDependencies(popHandle, meshHandle).Complete();

			var chunkComp = SystemAPI.GetComponent<ChunkComponent>(chunkEntity);

			if (localPos.x < 0 || localPos.x >= VoxelData.CHUNK_SIZE || localPos.y < 0 ||
			    localPos.y >= VoxelData.CHUNK_SIZE || localPos.z < 0 || localPos.z >= VoxelData.CHUNK_SIZE) return;

			chunkComp.BlockData.SetAtIndex(localPos.x, localPos.y, localPos.z, newBlockID);

			if (newBlockID.ID != 0) ecb.RemoveComponent<IsEmpty>(chunkEntity);

			ecb.AddComponent<NeedsMeshSync>(chunkEntity);

			switch (localPos.x)
			{
				case 0:
					TryMarkNeighbor(chunkCoord + new int3(-1, 0, 0), ecb);
					break;
				case VoxelData.CHUNK_SIZE - 1:
					TryMarkNeighbor(chunkCoord + new int3(1, 0, 0), ecb);
					break;
			}

			switch (localPos.y)
			{
				case 0:
					TryMarkNeighbor(chunkCoord + new int3(0, -1, 0), ecb);
					break;
				case VoxelData.CHUNK_SIZE - 1:
					TryMarkNeighbor(chunkCoord + new int3(0, 1, 0), ecb);
					break;
			}

			switch (localPos.z)
			{
				case 0:
					TryMarkNeighbor(chunkCoord + new int3(0, 0, -1), ecb);
					break;
				case VoxelData.CHUNK_SIZE - 1:
					TryMarkNeighbor(chunkCoord + new int3(0, 0, 1), ecb);
					break;
			}
		}

		private void TryMarkNeighbor(int3 neighborCoord, EntityCommandBuffer ecb)
		{
			if (SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap.TryGetValue(neighborCoord, out Entity chunkEntity))
			{
				ecb.AddComponent<NeedsMeshSync>(chunkEntity);
			}
		}
	}
}