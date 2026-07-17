using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace _Project.WorldGeneration.Systems
{
	/// <summary>
	///     Graph-based occlusion culling (Sodium-style). Flood-fills outward from the
	///     player's chunk through per-chunk face-connectivity masks (<see cref="ChunkOcclusion" />,
	///     built at mesh time), marking every chunk with an open sightline into a
	///     <see cref="ChunkBitTree" />. Chunks the flood never reaches (no line of sight
	///     through the terrain — behind hills, sealed caves, past a wall) are hidden by
	///     toggling <see cref="DisableRendering" /> on their render companions.
	///
	///     Frustum and per-instance culling stay with Entities Graphics; this only adds the
	///     "can you actually see through the world to get here" test that frustum culling
	///     can't. Because sightline reachability doesn't change when you merely rotate, the
	///     flood is rebuilt only when the player crosses a chunk boundary — near-free while
	///     stationary.
	///
	///     v1 limitations (safe, documented): uses only the outward-only "regular" variant
	///     (Sodium also runs WIDE/LOCAL to fix concave edge cases), so it can over-cull in
	///     rare concave sightlines; and a chunk meshed while the player stands still stays
	///     rendered until the next boundary crossing (over-render, never under-render).
	/// </summary>
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	[UpdateAfter(typeof(ChunkRenderUploadSystem))]
	public partial class ChunkOcclusionSystem : SystemBase
	{
		private ChunkBitTree tree;
		private int3         lastPlayerChunk;
		private EntityQuery  renderedQuery;

		protected override void OnCreate()
		{
			RequireForUpdate<Player>();
			RequireForUpdate<ChunkMapSingleton>();

			tree            = new ChunkBitTree(Allocator.Persistent);
			lastPlayerChunk = new int3(int.MaxValue);

			renderedQuery = GetEntityQuery(
			                               ComponentType.ReadOnly<ChunkManagedMesh>(),
			                               ComponentType.ReadOnly<ChunkPositionComponent>());
		}

		protected override void OnDestroy()
		{
			if (tree.IsCreated) tree.Dispose();
		}

		protected override void OnUpdate()
		{
			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
			                                                            SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = Utility.WorldToChunkCoord(playerPos);

			// Sightline reachability only changes when the camera moves to a new chunk.
			if (playerChunk.Equals(lastPlayerChunk)) return;
			lastPlayerChunk = playerChunk;

			// ── 1. Rebuild the visible set via outward BFS. ──
			ComponentLookup<ChunkOcclusion> occ = SystemAPI.GetComponentLookup<ChunkOcclusion>(true);
			occ.Update(this);

			tree.ResetTo(playerChunk);

			var bfs = new OcclusionBfsJob
			          {
				          ChunkMap    = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap,
				          Occlusion   = occ,
				          PlayerChunk = playerChunk,
				          ViewDist    = GameSettings.ViewDistanceInChunks,
				          Tree        = tree
			          };
			bfs.Schedule().Complete();

			// ── 2. Reconcile DisableRendering against the visible set. ──
			Reconcile();
		}

		private void Reconcile()
		{
			NativeArray<Entity> ents = renderedQuery.ToEntityArray(Allocator.Temp);
			var                 ecb  = new EntityCommandBuffer(Allocator.Temp);

			for (var i = 0; i < ents.Length; i++)
			{
				Entity e     = ents[i];
				var    mm    = EntityManager.GetComponentData<ChunkManagedMesh>(e);
				int3   coord = EntityManager.GetComponentData<ChunkPositionComponent>(e).ChunkCoord;

				var desired = tree.TestWorld(coord);
				ToggleRender(mm.SolidEntity, desired, ecb);
				ToggleRender(mm.FluidEntity, desired, ecb);
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
			ents.Dispose();
		}

		private void ToggleRender(Entity renderEntity, bool desired, EntityCommandBuffer ecb)
		{
			if (renderEntity == Entity.Null || !EntityManager.Exists(renderEntity)) return;

			var hidden = EntityManager.HasComponent<DisableRendering>(renderEntity);
			if (desired && hidden) ecb.RemoveComponent<DisableRendering>(renderEntity);
			else if (!desired && !hidden) ecb.AddComponent<DisableRendering>(renderEntity);
		}
	}

	[BurstCompile]
	internal struct OcclusionBfsJob : IJob
	{
		[ReadOnly] public NativeHashMap<int3, Entity>     ChunkMap;
		[ReadOnly] public ComponentLookup<ChunkOcclusion> Occlusion;

		public int3         PlayerChunk;
		public int          ViewDist;
		public ChunkBitTree Tree; // by value — Set() mutates shared pointer-backed memory

		// Mask with every from→to face pair set (fully transparent / not-yet-meshed chunk).
		private const ulong AllFacesConnected =
			(0x3Ful << 0)  | (0x3Ful << 8)  | (0x3Ful << 16) |
			(0x3Ful << 24) | (0x3Ful << 32) | (0x3Ful << 40);

		public void Execute()
		{
			var dim   = ChunkBitTree.DIM;
			var seen  = new NativeArray<byte>(dim * dim * dim, Allocator.Temp, NativeArrayOptions.ClearMemory);
			var queue = new NativeQueue<int>(Allocator.Temp);

			// Camera's own chunk: always visible, can look out through all 6 faces.
			if (Tree.TryToLocal(PlayerChunk, out int3 startLocal))
			{
				Tree.Set(startLocal);
				for (var f = 0; f < 6; f++)
					TryVisit(PlayerChunk, f, seen, ref queue);
			}

			while (queue.TryDequeue(out var packed))
			{
				var li     = packed >> 3;
				var inFace = packed & 7;

				var  local = new int3(li & 63, (li >> 6) & 63, (li >> 12) & 63);
				int3 coord = Tree.ToWorld(local);

				var mask = AllFacesConnected;
				if (ChunkMap.TryGetValue(coord, out Entity e) && Occlusion.HasComponent(e))
					mask = Occlusion[e].Mask;

				// Faces reachable from the one we entered through.
				var outFaces = (int)((mask >> (inFace * 8)) & 0x3F);
				for (var f = 0; f < 6; f++)
					if ((outFaces & (1 << f)) != 0)
						TryVisit(coord, f, seen, ref queue);
			}

			seen.Dispose();
			queue.Dispose();
		}

		private void TryVisit(int3 fromCoord, int face, NativeArray<byte> seen, ref NativeQueue<int> queue)
		{
			// NOTE: no outward-only constraint. Sodium's REGULAR variant forbids stepping
			// back toward the camera plane, which is fast but over-culls the concave
			// sightlines that hills/mountains/caves produce — chunks you can plainly see
			// get hidden (holes in the terrain from a high vantage). A full connectivity
			// flood never hides a chunk with a real line of sight through open air, and
			// still culls whatever is sealed behind solid rock (caves, behind a mountain).
			int3 nb = fromCoord + FaceOffset(face);

			// View-distance cap (Chebyshev).
			int3 d = math.abs(nb - PlayerChunk);
			if (math.cmax(d) > ViewDist) return;

			if (!Tree.TryToLocal(nb, out int3 nbLocal)) return;

			var inFace = face ^ 1; // we enter the neighbor through its opposite face
			var li     = (nbLocal.z * 64 + nbLocal.y) * 64 + nbLocal.x;
			if ((seen[li] & (1 << inFace)) != 0) return;
			seen[li] = (byte)(seen[li] | (1 << inFace));

			// Only loaded chunks render and can be traversed through; unloaded frontier
			// chunks aren't marked (nothing to draw, unknown connectivity).
			if (!ChunkMap.ContainsKey(nb)) return;

			Tree.Set(nbLocal);
			queue.Enqueue(li * 8 + inFace);
		}

		private static int3 FaceOffset(int face)
		{
			return face switch
			       {
				       0 => new int3(-1, 0, 0),
				       1 => new int3(1, 0, 0),
				       2 => new int3(0, -1, 0),
				       3 => new int3(0, 1, 0),
				       4 => new int3(0, 0, -1),
				       _ => new int3(0, 0, 1)
			       };
		}
	}
}
