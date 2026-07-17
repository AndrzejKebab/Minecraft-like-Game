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
	///     The committed occlusion result, shared with ChunkRenderUploadSystem so a chunk
	///     meshed while inside a hidden region spawns hidden instead of flashing visible
	///     until the next pass. Front is only ever swapped on the main thread after the
	///     BFS completes — in-flight jobs write the back tree, never this one.
	/// </summary>
	public struct ChunkOcclusionTreeSingleton : IComponentData
	{
		public ChunkBitTree Front;
		public bool         Valid; // false until the first pass has completed
	}

	/// <summary>
	///     Graph-based occlusion culling (Sodium-style). Flood-fills outward from the
	///     player's chunk through per-chunk face-connectivity masks (<see cref="ChunkOcclusion" />,
	///     computed at populate time from BlockData and refreshed on re-mesh), marking every
	///     chunk with an open sightline into a <see cref="ChunkBitTree" />. Chunks the flood
	///     never reaches (behind hills, sealed caves, past a wall) are hidden by toggling
	///     <see cref="DisableRendering" /> on their render companions.
	///
	///     Frustum and per-instance culling stay with Entities Graphics; this only adds the
	///     "can you actually see through the world to get here" test that frustum culling
	///     can't answer. The result is rotation-invariant by design, so rebuilds happen on
	///     chunk-boundary crossings and (throttled) whenever chunk masks change — newly
	///     generated terrain is culled without waiting for the player to move.
	///
	///     The pass is fully async (snapshot → background BFS → reconcile on completion).
	///     Reconcile is a DIFF against the previous pass: the two bit-trees are compared
	///     row-by-row (origin shift = an x bit-shift + z/y row offset), so only chunks whose
	///     visibility actually flipped pay a structural change — not the whole render set.
	/// </summary>
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	[UpdateAfter(typeof(ChunkRenderUploadSystem))]
	public partial struct ChunkOcclusionSystem : ISystem
	{
		/// <summary> Min seconds between mask-change re-floods (crossings re-flood immediately). </summary>
		private const float REFLOOD_INTERVAL = 0.25f;

		private ChunkBitTree               frontTree; // committed result (enforced on entities)
		private ChunkBitTree               backTree;  // written by the in-flight BFS
		private NativeHashMap<int3, ulong> snapshot;  // reused per-pass coord → mask snapshot
		private int3                       lastPlayerChunk;
		private EntityQuery                renderedQuery;
		private EntityQuery                maskChangedQuery; // changed-version filter on ChunkOcclusion

		private JobHandle bfsHandle;
		private bool      jobRunning;
		private bool      firstPassDone;
		private bool      masksDirty;
		private double    lastFloodTime;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<ChunkMapSingleton>();

			frontTree       = new ChunkBitTree(Allocator.Persistent);
			backTree        = new ChunkBitTree(Allocator.Persistent);
			lastPlayerChunk = new int3(int.MaxValue);

			var diameter = GameSettings.ViewDistanceInChunks * 2 + 1;
			snapshot = new NativeHashMap<int3, ulong>(diameter * diameter * diameter, Allocator.Persistent);

			renderedQuery = SystemAPI.QueryBuilder()
			                         .WithAll<ChunkManagedMesh, ChunkPositionComponent>()
			                         .Build();

			// matches only chunks whose ChunkOcclusion was added/updated since our last run —
			// the "terrain changed, the visible set is stale" signal
			maskChangedQuery = SystemAPI.QueryBuilder().WithAll<ChunkOcclusion>().Build();
			maskChangedQuery.SetChangedVersionFilter(ComponentType.ReadOnly<ChunkOcclusion>());

			state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(),
			                                     new ChunkOcclusionTreeSingleton { Front = frontTree, Valid = false });
		}

		public void OnDestroy(ref SystemState state)
		{
			if (jobRunning) bfsHandle.Complete();
			if (frontTree.IsCreated) frontTree.Dispose();
			if (backTree.IsCreated) backTree.Dispose();
			if (snapshot.IsCreated) snapshot.Dispose();
		}

		public void OnUpdate(ref SystemState state)
		{
			// Accumulate the dirty signal every frame (the changed filter compares against
			// this system's last run, so it must be sampled even while a pass is in flight).
			masksDirty |= !maskChangedQuery.IsEmpty;

			// ── 1. Finish an in-flight pass without stalling. ──
			// Poll IsCompleted; only when the BFS finished on its own do we Complete()
			// (cheap — releases the fence), apply the diff, and commit the new tree.
			if (jobRunning)
			{
				if (!bfsHandle.IsCompleted) return;
				bfsHandle.Complete();
				jobRunning = false;

				if (firstPassDone)
				{
					DiffReconcile(ref state);
				}
				else
				{
					FullReconcile(ref state); // no enforced previous state to diff against
					firstPassDone = true;
				}

				(frontTree, backTree) = (backTree, frontTree);
				SystemAPI.SetSingleton(new ChunkOcclusionTreeSingleton { Front = frontTree, Valid = true });
			}

			float3 playerPos = SystemAPI.GetComponentRO<LocalTransform>(
			                                                            SystemAPI.GetSingletonEntity<Player>()).ValueRO
			                            .Position;
			int3 playerChunk = Utility.WorldToChunkCoord(playerPos);

			// ── 2. Decide whether a new pass is due. ──
			// Crossing a chunk boundary changes reachability → re-flood immediately.
			// Mask changes (terrain generated / edited) → re-flood, throttled.
			var    crossed = !playerChunk.Equals(lastPlayerChunk);
			double now     = SystemAPI.Time.ElapsedTime;
			if (!crossed && !(masksDirty && now - lastFloodTime >= REFLOOD_INTERVAL)) return;
			lastPlayerChunk = playerChunk;

			// ── 3. Snapshot inputs on the main thread, then schedule the BFS async. ──
			BuildSnapshot(ref state, playerChunk);
			backTree.ResetTo(playerChunk);

			bfsHandle = new OcclusionBfsJob
			            {
				            Masks       = snapshot,
				            PlayerChunk = playerChunk,
				            ViewDist    = GameSettings.ViewDistanceInChunks,
				            Tree        = backTree
			            }.Schedule();
			jobRunning    = true;
			masksDirty    = false;
			lastFloodTime = now;
		}

		/// <summary>
		///     Copy every loaded chunk within view distance into <see cref="snapshot" /> as
		///     coord → visibility mask. Populated chunks always have a real mask (produced by
		///     ChunkPopulateJob, refreshed by GreedyMeshJob); a chunk without one is still
		///     generating — treated as OPAQUE (0), so streaming regions never leak sightlines
		///     to terrain the player can't actually see. The BFS reads only this private
		///     copy, so it can run across frames while the simulation group mutates the live
		///     ChunkMap without a job-safety conflict.
		/// </summary>
		private void BuildSnapshot(ref SystemState state, int3 playerChunk)
		{
			snapshot.Clear();

			ComponentLookup<ChunkOcclusion> occ = SystemAPI.GetComponentLookup<ChunkOcclusion>(true);
			occ.Update(ref state);

			int                         viewDist = GameSettings.ViewDistanceInChunks;
			NativeHashMap<int3, Entity> map      = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;

			foreach (KVPair<int3, Entity> kv in map)
			{
				if (math.cmax(math.abs(kv.Key - playerChunk)) > viewDist) continue;
				var mask = occ.HasComponent(kv.Value) ? occ[kv.Value].Mask : 0ul;
				snapshot.TryAdd(kv.Key, mask);
			}
		}

		/// <summary>
		///     Toggle only chunks whose visibility flipped between the committed front tree
		///     and the just-finished back tree. The trees may have different origins (the
		///     player moved), so front rows are fetched at the shifted (z, y) and their x
		///     bits aligned with a shift; XOR then yields exactly the changed chunks.
		/// </summary>
		private void DiffReconcile(ref SystemState state)
		{
			NativeHashMap<int3, Entity> map = SystemAPI.GetSingleton<ChunkMapSingleton>().ChunkMap;
			EntityManager               em  = state.EntityManager;
			var                         ecb = new EntityCommandBuffer(Allocator.Temp);

			int3 delta    = backTree.Origin - frontTree.Origin; // front local = back local + delta
			var  xInRange = math.abs(delta.x) < ChunkBitTree.DIM;

			for (var z = 0; z < ChunkBitTree.DIM; z++)
			for (var y = 0; y < ChunkBitTree.DIM; y++)
			{
				var backRow  = backTree.Row(z, y);
				var frontRow = xInRange ? frontTree.Row(z + delta.z, y + delta.y) : 0ul;
				var aligned  = delta.x >= 0 ? frontRow >> delta.x : frontRow << -delta.x;

				var changed = aligned ^ backRow;
				while (changed != 0)
				{
					var x = math.tzcnt(changed);
					changed &= changed - 1;

					int3 world   = backTree.ToWorld(new int3(x, y, z));
					var  desired = (backRow & (1ul << x)) != 0;

					if (!map.TryGetValue(world, out Entity e)) continue;
					if (!em.HasComponent<ChunkManagedMesh>(e)) continue;

					var mm = em.GetComponentData<ChunkManagedMesh>(e);
					ToggleRender(em, mm.SolidEntity, desired, ecb);
					ToggleRender(em, mm.FluidEntity, desired, ecb);
				}
			}

			ecb.Playback(em);
			ecb.Dispose();
		}

		/// <summary>
		///     Walk the whole rendered set and enforce the back tree. Only used for the very
		///     first pass, where no previously-enforced state exists to diff against.
		/// </summary>
		private void FullReconcile(ref SystemState state)
		{
			EntityManager       em   = state.EntityManager;
			NativeArray<Entity> ents = renderedQuery.ToEntityArray(Allocator.Temp);
			var                 ecb  = new EntityCommandBuffer(Allocator.Temp);

			for (var i = 0; i < ents.Length; i++)
			{
				Entity e     = ents[i];
				var    mm    = em.GetComponentData<ChunkManagedMesh>(e);
				int3   coord = em.GetComponentData<ChunkPositionComponent>(e).ChunkCoord;

				var desired = backTree.TestWorld(coord);
				ToggleRender(em, mm.SolidEntity, desired, ecb);
				ToggleRender(em, mm.FluidEntity, desired, ecb);
			}

			ecb.Playback(em);
			ecb.Dispose();
			ents.Dispose();
		}

		private static void ToggleRender(EntityManager em, Entity renderEntity, bool desired, EntityCommandBuffer ecb)
		{
			if (renderEntity == Entity.Null || !em.Exists(renderEntity)) return;

			var hidden = em.HasComponent<DisableRendering>(renderEntity);
			switch (desired)
			{
				case true when hidden:
					ecb.RemoveComponent<DisableRendering>(renderEntity);
					break;
				case false when !hidden:
					ecb.AddComponent<DisableRendering>(renderEntity);
					break;
			}
		}
	}

	/// <summary>
	///     Sodium's graph-occlusion traversal, ported to this face order
	///     (0=X-,1=X+,2=Y-,3=Y+,4=Z-,5=Z+; opposite = face^1). A relaxing BFS over chunk
	///     coords: each chunk carries a 64-bit visibility mask (bit <c>from*8+to</c> = "a
	///     sightline entering through face <c>from</c> can leave through face <c>to</c>"),
	///     flood-filled from the chunk's own BlockData at populate time.
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
	///     not exact camera position/frustum), so the flood is stable under pure rotation.
	///     Flip to WIDE (WIDTH=1, SLACK=3) if a concave vantage (mountain ridge, cliff
	///     mouth) ever shows a hole.
	/// </summary>
	[BurstCompile]
	internal struct OcclusionBfsJob : IJob
	{
		// Private per-pass snapshot: loaded chunk coord → its visibility mask (0 = still
		// generating, treated as opaque). Presence in the map means "loaded". Snapshotting
		// on the main thread at schedule time lets this job run across frames without
		// racing the live ChunkMap/ComponentLookup that the simulation group mutates — so
		// it never has to be force-completed synchronously.
		[ReadOnly] public NativeHashMap<int3, ulong> Masks;

		public int3         PlayerChunk;
		public int          ViewDist;
		public ChunkBitTree Tree; // by value — Set() mutates shared pointer-backed memory

		// REGULAR variant. Set WIDTH=1, SLACK=3 for Sodium's WIDE fallback.
		private const int WIDTH = 0;
		private const int SLACK = 1;

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
				// coord absent from the snapshot has unknown connectivity and nothing to render.
				if (!Masks.TryGetValue(coord, out var mask)) continue;

				Tree.Set(local);

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
