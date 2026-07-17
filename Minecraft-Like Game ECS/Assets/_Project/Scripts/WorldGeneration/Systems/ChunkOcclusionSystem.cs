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

	/// <summary>
	///     Sodium's graph-occlusion traversal, ported to this face order
	///     (0=X-,1=X+,2=Y-,3=Y+,4=Z-,5=Z+; opposite = face^1). A relaxing BFS over chunk
	///     coords: each chunk carries a 64-bit visibility mask (bit <c>from*8+to</c> = "a
	///     sightline entering through face <c>from</c> can leave through face <c>to</c>"),
	///     built at mesh time by flood-filling the chunk's own transparent cells.
	///
	///     Two constraints keep it from over-expanding into chunks you can't actually see,
	///     exactly as Sodium does:
	///
	///     • <b>Outward directions</b> — the flood may only step away from the camera plane
	///       on each axis (monotonic expansion). WIDTH widens this band by a chunk so the
	///       camera's own neighbourhood isn't clipped.
	///     • <b>Angle visibility mask</b> — a chunk far off the dominant camera→chunk axis
	///       can't be seen straight-through on the minor axes, so those straight-through
	///       face pairs are removed from its mask. SLACK controls how eagerly.
	///
	///     REGULAR (WIDTH=0, SLACK=1) is rotation-invariant (depends only on chunk coords,
	///     not exact camera position/frustum), so the flood is stable under pure rotation
	///     and only needs rebuilding on a chunk-boundary crossing. Flip to WIDE (WIDTH=1,
	///     SLACK=3) if a concave vantage (mountain ridge, cliff mouth) ever shows a hole.
	/// </summary>
	[BurstCompile]
	internal struct OcclusionBfsJob : IJob
	{
		[ReadOnly] public NativeHashMap<int3, Entity>     ChunkMap;
		[ReadOnly] public ComponentLookup<ChunkOcclusion> Occlusion;

		public int3         PlayerChunk;
		public int          ViewDist;
		public ChunkBitTree Tree; // by value — Set() mutates shared pointer-backed memory

		// REGULAR variant. Set WIDTH=1, SLACK=3 for Sodium's WIDE fallback.
		private const int WIDTH = 0;
		private const int SLACK = 1;

		// Mask with every from→to face pair set (fully transparent / not-yet-meshed chunk).
		private const ulong AllFacesConnected =
			(0x3Ful << 0)  | (0x3Ful << 8)  | (0x3Ful << 16) |
			(0x3Ful << 24) | (0x3Ful << 32) | (0x3Ful << 40);

		// Straight-through face pairs per axis (from*8+to), in this face order.
		private const ulong X_THROUGH = (1ul << (0 * 8 + 1)) | (1ul << (1 * 8 + 0));
		private const ulong Y_THROUGH = (1ul << (2 * 8 + 3)) | (1ul << (3 * 8 + 2));
		private const ulong Z_THROUGH = (1ul << (4 * 8 + 5)) | (1ul << (5 * 8 + 4));

		public void Execute()
		{
			var dim   = ChunkBitTree.DIM;
			var count = dim * dim * dim;

			// Per-cell set of incoming face-directions already accounted for. Growing this
			// set is what re-queues a node (relaxation): a chunk reached from a new side may
			// expose new outgoing sightlines its earlier visit couldn't.
			var incoming = new NativeArray<byte>(count, Allocator.Temp, NativeArrayOptions.ClearMemory);
			var queue    = new NativeQueue<int>(Allocator.Temp);

			if (!Tree.TryToLocal(PlayerChunk, out int3 startLocal))
			{
				incoming.Dispose();
				queue.Dispose();
				return;
			}

			// Camera's own chunk: visible, seeded as if lit from every side so the flood can
			// leave through any open face.
			var startLi = (startLocal.z * dim + startLocal.y) * dim + startLocal.x;
			incoming[startLi] = 0x3F;
			queue.Enqueue(startLi);

			while (queue.TryDequeue(out var li))
			{
				var  local = new int3(li % dim, (li / dim) % dim, li / (dim * dim));
				int3 coord = Tree.ToWorld(local);

				int inc = incoming[li];

				// Loaded chunks are the only ones we draw and traverse through; a frontier
				// coord not in the map has unknown connectivity and nothing to render.
				var loaded = ChunkMap.TryGetValue(coord, out Entity e);
				if (!loaded) continue;

				Tree.Set(local);

				ulong mask = Occlusion.HasComponent(e) ? Occlusion[e].Mask : AllFacesConnected;
				mask &= AngleMask(coord);

				int outgoing = GetConnections(mask, inc) & OutwardDirs(coord);

				for (var f = 0; f < 6; f++)
				{
					if ((outgoing & (1 << f)) == 0) continue;

					int3 nb = coord + FaceOffset(f);
					if (math.cmax(math.abs(nb - PlayerChunk)) > ViewDist) continue;
					if (!Tree.TryToLocal(nb, out int3 nbLocal)) continue;

					var nbLi   = (nbLocal.z * dim + nbLocal.y) * dim + nbLocal.x;
					var inFace = 1 << (f ^ 1); // enter the neighbour through its opposite face
					var newInc = incoming[nbLi] | inFace;
					if (newInc == incoming[nbLi]) continue; // no new sightline — skip

					incoming[nbLi] = (byte)newInc;
					queue.Enqueue(nbLi);
				}
			}

			incoming.Dispose();
			queue.Dispose();
		}

		/// <summary> Fold the rows selected by the incoming set into a 6-bit outgoing set. </summary>
		private static int GetConnections(ulong mask, int incoming)
		{
			ulong rows = mask & RowMask(incoming);
			rows |= rows >> 32;
			rows |= rows >> 16;
			rows |= rows >> 8;
			return (int)(rows & 0x3F);
		}

		/// <summary> Expand each set incoming face-bit into its full 6-bit row in the mask. </summary>
		private static ulong RowMask(int incoming)
		{
			ulong m = 0;
			for (var i = 0; i < 6; i++)
				if ((incoming & (1 << i)) != 0)
					m |= 0x3Ful << (i * 8);
			return m;
		}

		/// <summary>
		///     Remove straight-through pairs on any axis that isn't the dominant camera→chunk
		///     axis: a chunk well off to one side can't be seen straight-through on the minor
		///     axes, so those sightlines are occluded.
		/// </summary>
		private ulong AngleMask(int3 coord)
		{
			int dx = math.abs(coord.x - PlayerChunk.x);
			int dy = math.abs(coord.y - PlayerChunk.y);
			int dz = math.abs(coord.z - PlayerChunk.z);

			ulong occ = 0;
			if (dy > dx + SLACK || dz > dx + SLACK) occ |= X_THROUGH;
			if (dx > dy + SLACK || dz > dy + SLACK) occ |= Y_THROUGH;
			if (dx > dz + SLACK || dy > dz + SLACK) occ |= Z_THROUGH;
			return ~occ;
		}

		/// <summary> Faces that point away from the camera plane (monotonic expansion). </summary>
		private int OutwardDirs(int3 coord)
		{
			int rx = coord.x - PlayerChunk.x;
			int ry = coord.y - PlayerChunk.y;
			int rz = coord.z - PlayerChunk.z;

			var d = 0;
			if (rx <= WIDTH) d |= 1 << 0;  // X-
			if (rx >= -WIDTH) d |= 1 << 1; // X+
			if (ry <= WIDTH) d |= 1 << 2;  // Y-
			if (ry >= -WIDTH) d |= 1 << 3; // Y+
			if (rz <= WIDTH) d |= 1 << 4;  // Z-
			if (rz >= -WIDTH) d |= 1 << 5; // Z+
			return d;
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
