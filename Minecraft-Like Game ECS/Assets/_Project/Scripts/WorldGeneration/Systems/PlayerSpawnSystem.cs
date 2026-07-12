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
	///        velocity every frame) while chunks stream in; the exact surface Y is
	///        read from the generated tile columns (post-erosion ground truth).
	///     3. Release — when the ground chunk has its physics collider, or after a
	///        hard timeout so the player can never be locked in place forever.
	///     Runs before PlayerVisibleChunksSystem so streaming centres on the spawn
	///     from the very first frame.
	/// </summary>
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
	[UpdateBefore(typeof(PlayerVisibleChunksSystem))]
	public partial struct PlayerSpawnSystem : ISystem
	{
		private const int HOLD_TIMEOUT_FRAMES  = 1800; // ~30 s — never lock movement forever
		private const int GUARD_TIMEOUT_FRAMES = 900;  // post-release fall watch

		private byte phase; // 0 = search, 1 = hold, 2 = fall guard, 3 = done
		private bool groundRefined;
		private int  holdFrames;
		private int  guardFrames;
		private int3 spawnBlock; // top solid block of the spawn column

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<TerraGenSettings>();
			state.RequireForUpdate<TerraTileCacheSingleton>();
			state.RequireForUpdate<ChunkMapSingleton>();
		}

		public void OnDestroy(ref SystemState state) { }

		public void OnUpdate(ref SystemState state)
		{
			if (phase == 3)
			{
				state.Enabled = false;
				return;
			}

			Entity player = SystemAPI.GetSingletonEntity<Player>();

			// ── 4. fall guard: after release, catch the player if the collider
			//      still wasn't ready and they slipped through the ground ────────
			if (phase == 2)
			{
				guardFrames++;
				var pos = SystemAPI.GetComponentRO<LocalTransform>(player).ValueRO.Position;
				if (pos.y < spawnBlock.y - 20f)
				{
					Debug.LogWarning("[PlayerSpawnSystem] Player fell through unready ground — " +
					                 "teleporting back to the spawn");
					SystemAPI.GetComponentRW<LocalTransform>(player).ValueRW.Position =
						new float3(spawnBlock.x + 0.5f, spawnBlock.y + 2.5f, spawnBlock.z + 0.5f);
					if (SystemAPI.HasComponent<KinematicCharacterBody>(player))
						SystemAPI.GetComponentRW<KinematicCharacterBody>(player).ValueRW.RelativeVelocity =
							float3.zero;
				}

				if (guardFrames > GUARD_TIMEOUT_FRAMES) phase = 3;
				return;
			}

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

			// ── 2. refine surface height from the generated tile (once) ────────
			//     tile columns are the post-erosion ground truth the chunks are
			//     built from — no need to touch chunk block data
			if (!groundRefined)
			{
				var  cache     = SystemAPI.GetSingleton<TerraTileCacheSingleton>();
				int3 chunk     = Utility.WorldToChunkCoord(new float3(spawnBlock.x, 0f, spawnBlock.z));
				int2 tileCoord = TerraTileConst.TileOfChunk(new int2(chunk.x, chunk.z));
				if (cache.Tiles.TryGetValue(tileCoord, out TerraTile tile) && tile.GenHandle.IsCompleted)
				{
					tile.GenHandle.Complete(); // clears the write dependency for main-thread read
					int2 origin = TerraTileConst.GenOrigin(tileCoord);
					var index = spawnBlock.x - origin.x +
					            (spawnBlock.z - origin.y) * TerraTileConst.GEN_BLOCKS;
					spawnBlock.y  = tile.Columns[index].SurfaceY;
					groundRefined = true;
				}
			}

			// ── hold: pin position + zero velocity until the ground is solid ───
			var spawnPos = new float3(spawnBlock.x + 0.5f, spawnBlock.y + 2.5f, spawnBlock.z + 0.5f);
			SystemAPI.GetComponentRW<LocalTransform>(player).ValueRW.Position = spawnPos;
			if (SystemAPI.HasComponent<KinematicCharacterBody>(player))
				SystemAPI.GetComponentRW<KinematicCharacterBody>(player).ValueRW.RelativeVelocity =
					float3.zero;

			// ── 3. release ─────────────────────────────────────────────────────
			holdFrames++;
			if (holdFrames > HOLD_TIMEOUT_FRAMES)
			{
				Debug.LogWarning("[PlayerSpawnSystem] Ground collider never appeared — " +
				                 "releasing the player with a fall guard");
				phase = 2;
				return;
			}

			if (!groundRefined) return;

			var  map         = SystemAPI.GetSingleton<ChunkMapSingleton>();
			int3 groundChunk = Utility.WorldToChunkCoord(
				new float3(spawnBlock.x, spawnBlock.y, spawnBlock.z));
			if (!map.ChunkMap.TryGetValue(groundChunk, out Entity groundEntity)) return;

			if (SystemAPI.HasComponent<PhysicsCollider>(groundEntity) ||
			    SystemAPI.HasComponent<HasCollider>(groundEntity))
			{
				Debug.Log($"[PlayerSpawnSystem] Ground ready at {spawnBlock}, releasing " +
				          $"after {holdFrames} frames");
				phase = 2;
				return;
			}

			// ground chunk is populated but has no collider yet — request an urgent
			// bake (ChunkCollidersSystem completes urgent batches synchronously)
			if (SystemAPI.HasComponent<IsPopulated>(groundEntity) &&
			    !SystemAPI.HasComponent<UrgentColliderSync>(groundEntity))
				state.EntityManager.AddComponent<UrgentColliderSync>(groundEntity);
		}
	}
}
