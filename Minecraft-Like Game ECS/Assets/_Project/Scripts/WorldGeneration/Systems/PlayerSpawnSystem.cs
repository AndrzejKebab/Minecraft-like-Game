using _Project.Tags;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.TerraGen;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace _Project.WorldGeneration.Systems
{
	/// <summary>
	///     Relocates the player from their scene-authored position to a proper spawn
	///     on land (RTF SpawnFinder equivalent — see TerraSpawnSearchJob), so the
	///     spawn no longer ends up underground or high in the air depending on seed.
	///     Three phases:
	///     1. Search  — one-shot Burst spiral search over the heightmap pipeline.
	///     2. Hold    — pin the player above the spawn column (position + zeroed
	///        velocity every frame) while chunks stream in around it; once the
	///        ground chunk is populated, refine the exact surface Y from real block
	///        data (the droplet erosion shifts the sampled estimate a few blocks).
	///     3. Release — when the ground chunk has its physics collider, stop
	///        touching the player and disable the system.
	///     Runs before PlayerVisibleChunksSystem so streaming centres on the spawn
	///     from the very first frame.
	/// </summary>
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
	[UpdateBefore(typeof(PlayerVisibleChunksSystem))]
	public partial struct PlayerSpawnSystem : ISystem
	{
		private byte phase; // 0 = search, 1 = hold, 2 = done
		private bool groundRefined;
		private int3 spawnBlock; // top solid block of the spawn column

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<TerraGenSettings>();
			state.RequireForUpdate<ChunkMapSingleton>();
		}

		public void OnDestroy(ref SystemState state) { }

		public void OnUpdate(ref SystemState state)
		{
			if (phase == 2)
			{
				state.Enabled = false;
				return;
			}

			Entity player = SystemAPI.GetSingletonEntity<Player>();

			// ── 1. search ──────────────────────────────────────────────────────
			if (phase == 0)
			{
				var result = new NativeArray<int4>(1, Allocator.TempJob);
				new TerraSpawnSearchJob
				{
					Settings = SystemAPI.GetSingleton<TerraGenSettings>(),
					Result   = result
				}.Schedule().Complete();

				int4 found = result[0];
				result.Dispose();

				spawnBlock = new int3(found.x, found.y, found.z);
				phase      = 1;
				Debug.Log($"[PlayerSpawnSystem] Spawning at {spawnBlock} " +
				          $"(criteria matched: {found.w == 1})");
			}

			var map = SystemAPI.GetSingleton<ChunkMapSingleton>();

			// ── 2. refine surface height from real chunk data (once) ───────────
			if (!groundRefined && TryFindGroundY(ref state, in map, spawnBlock, out var groundY))
			{
				spawnBlock.y  = groundY;
				groundRefined = true;
			}

			// ── hold: pin position + zero velocity until the ground is solid ───
			var spawnPos = new float3(spawnBlock.x + 0.5f, spawnBlock.y + 2.5f, spawnBlock.z + 0.5f);
			SystemAPI.GetComponentRW<LocalTransform>(player).ValueRW.Position = spawnPos;
			if (SystemAPI.HasComponent<KinematicCharacterBody>(player))
				SystemAPI.GetComponentRW<KinematicCharacterBody>(player).ValueRW.RelativeVelocity =
					float3.zero;

			// ── 3. release once the ground chunk is collidable ─────────────────
			if (!groundRefined) return;
			int3 groundChunk = Utility.WorldToChunkCoord(
				new float3(spawnBlock.x, spawnBlock.y, spawnBlock.z));
			if (map.ChunkMap.TryGetValue(groundChunk, out Entity groundEntity) &&
			    SystemAPI.HasComponent<IsPopulated>(groundEntity) &&
			    SystemAPI.HasComponent<PhysicsCollider>(groundEntity))
				phase = 2;
		}

		/// <summary>
		///     Scans real block data around the estimated surface for the actual top
		///     solid block. Returns false while the needed chunks aren't populated
		///     yet (retry next frame). Falls back to the estimate if the window
		///     contains no solid block (e.g. a cave mouth).
		/// </summary>
		private bool TryFindGroundY(ref SystemState state, in ChunkMapSingleton map,
		                            int3 estimate, out int groundY)
		{
			const int window = 8; // erosion shifts surfaces by ±5 at most
			groundY = estimate.y;

			for (var y = estimate.y + window; y >= estimate.y - window; y--)
			{
				int3 chunkCoord = Utility.WorldToChunkCoord(new float3(estimate.x, y, estimate.z));
				if (!map.ChunkMap.TryGetValue(chunkCoord, out Entity chunkEntity)) return false;
				if (!SystemAPI.HasComponent<IsPopulated>(chunkEntity)) return false;

				// don't read block data while a populate/mesh job may still touch it
				if (SystemAPI.HasComponent<ChunkActiveJob>(chunkEntity) &&
				    !SystemAPI.GetComponent<ChunkActiveJob>(chunkEntity).Handle.IsCompleted)
					return false;

				if (!map.ChunkDataLookup.TryGetValue(chunkEntity, out ChunkComponent chunk)) return false;

				int3 local = new int3(estimate.x, y, estimate.z) - chunkCoord * ChunkData.CHUNK_SIZE;
				var  index = local.x | (local.y << 5) | (local.z << 10);
				if (chunk.BlockData[index].ID != 0)
				{
					groundY = y;
					return true;
				}
			}

			return true; // window is all air (cave mouth?) — keep the estimate
		}
	}
}
