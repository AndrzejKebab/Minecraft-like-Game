using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Jobs;
using FastNoise2.Bindings;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Systems
{
	/// <summary>
	/// Single system that owns the entire chunk population pipeline:
	///
	///   TerrainShapePassJob ──► CavesPassJob ──► DecorationPassJob ──► IsPopulated
	///
	/// <see cref="IsPopulated"/> is only added after ALL three passes complete,
	/// so no downstream system can ever read partially-generated block data.
	///
	/// DECORATION WAVE SCHEDULING
	/// ──────────────────────────
	/// Decoration jobs write into neighbouring chunks (tree canopies), so they
	/// cannot all run in parallel. We use a checkerboard colour scheme:
	///
	///     colour = PositiveMod(coord.x, 2) * 2 + PositiveMod(coord.z, 2)   (0–3)
	///
	/// Jobs of the same colour are ≥2 chunks apart in XZ, so their write zones
	/// never overlap. Four serial waves (one per colour) give us safe parallelism:
	///
	///   Wave 0 ──► Wave 1 ──► Wave 2 ──► Wave 3
	///       ↑ all jobs within a wave run in parallel
	///
	/// NEIGHBOUR READINESS
	/// ───────────────────
	/// Before a chunk can be decorated it needs its 8 horizontal neighbours and
	/// the chunk directly above to have finished the terrain+caves passes.
	/// We check both <see cref="_terrainReady"/> (chunks awaiting decoration)
	/// AND ECS <see cref="IsPopulated"/> chunks (fully done neighbours), so that
	/// an already-decorated neighbour never blocks a new chunk from proceeding.
	/// </summary>
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(PlayerVisibleChunksSystem))]
	public partial struct ChunkPopulateSystem : ISystem
	{
		// ── Terrain pass ──────────────────────────────────────────────────────

		/// <summary>In-flight terrain+caves jobs.</summary>
		private NativeList<ActiveTerrainJob> _terrainJobs;

		/// <summary>
		/// Chunks that finished terrain+caves but have not yet been decorated.
		/// Key = chunk coord (world / CHUNK_SIZE).
		/// Block data lives here — NOT in ECS — until decoration is done.
		/// </summary>
		private NativeHashMap<int3, TerrainReadyEntry> _terrainReady;

		// ── Decoration batch ──────────────────────────────────────────────────

		/// <summary>Shared block-data map used by all decoration jobs in the batch.</summary>
		private NativeParallelHashMap<int3, ChunkBlockDataRef> _chunkDataMap;

		/// <summary>Records which chunk coords received at least one decoration write.</summary>
		private NativeParallelHashMap<int3, bool> _dirtyMap;

		/// <summary>Chunk coords being decorated in the current batch.</summary>
		private NativeList<int3> _decorBatchCoords;

		private JobHandle _decorBatchHandle;
		private bool      _decorBatchActive;

		// ── FastNoise ─────────────────────────────────────────────────────────

		private FastNoise _noise;
		private bool      _noiseInit;

		// ── Lifecycle ─────────────────────────────────────────────────────────

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<WorldBlockRegistrySingleton>();
			state.RequireForUpdate<WorldSettingsSingleton>();

			_terrainJobs      = new NativeList<ActiveTerrainJob>(Allocator.Persistent);
			_terrainReady     = new NativeHashMap<int3, TerrainReadyEntry>(256, Allocator.Persistent);
			_decorBatchCoords = new NativeList<int3>(Allocator.Persistent);
		}

		public void OnDestroy(ref SystemState state)
		{
			if (_noiseInit && _noise.IsCreated) _noise.Dispose();

			// Complete and discard any running terrain jobs
			for (int i = 0; i < _terrainJobs.Count; i++)
			{
				_terrainJobs[i].Handle.Complete();
				_terrainJobs[i].BlockData.Dispose();
				_terrainJobs[i].IsDirty.Dispose();
			}
			_terrainJobs.Dispose();

			// Complete and discard decoration batch
			if (_decorBatchActive) _decorBatchHandle.Complete();
			DisposeBatchResources();
			_decorBatchCoords.Dispose();

			// Dispose block data for chunks still awaiting decoration
			using var keys = _terrainReady.GetKeyArray(Allocator.Temp);
			foreach (int3 key in keys)
			{
				var e = _terrainReady[key];
				if (e.BlockData.IsCreated) e.BlockData.Dispose();
			}
			_terrainReady.Dispose();
		}

		// ── OnUpdate ──────────────────────────────────────────────────────────

		public void OnUpdate(ref SystemState state)
		{
			if (!SystemAPI.TryGetSingleton(out WorldSettingsSingleton settings)) return;
			if (!_noiseInit) InitNoise(ref settings);

			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();
			var em       = state.EntityManager;

			// Step 1 — collect terrain+caves jobs that finished this frame
			ProcessTerrainJobs(em);

			// Step 2 — finalise a completed decoration batch
			if (_decorBatchActive && _decorBatchHandle.IsCompleted)
			{
				_decorBatchHandle.Complete();
				ApplyDecorationResults(ref state);
				DisposeBatchResources();
				_decorBatchActive = false;
			}

			// Step 3 — remove _terrainReady entries whose entity was destroyed
			CleanupDestroyedEntries(em);

			// Step 4 — schedule terrain+caves passes for newly visible chunks
			ScheduleTerrainJobs(ref state, ref em, ref settings, ref registry);

			// Step 5 — schedule decoration batch for chunks whose neighbours are ready
			//          (runs concurrently with terrain jobs; only one batch at a time)
			if (!_decorBatchActive)
				ScheduleDecorationBatch(ref state, em, ref settings, ref registry);
		}

		// ── Step 1: Process terrain jobs ──────────────────────────────────────

		private void ProcessTerrainJobs(EntityManager em)
		{
			for (int i = _terrainJobs.Count - 1; i >= 0; i--)
			{
				ActiveTerrainJob job = _terrainJobs[i];
				if (!job.Handle.IsCompleted) continue;

				job.Handle.Complete();

				if (em.Exists(job.Entity))
				{
					// Block data stays exclusively in _terrainReady until ALL passes
					// complete. Writing it into ChunkComponent here would create two
					// owners: if the entity is destroyed before decoration finishes,
					// the chunk manager disposes comp.BlockData and then
					// CleanupDestroyedEntries double-disposes the same pointer → crash.
					// It is assigned to ChunkComponent only in ApplyDecorationResults,
					// right before IsPopulated is added.
					_terrainReady.TryAdd(job.ChunkCoord, new TerrainReadyEntry
					{
						Entity    = job.Entity,
						BlockData = job.BlockData,
						IsDirty   = job.IsDirty.Value,
					});
				}
				else
				{
					job.BlockData.Dispose();
				}

				job.IsDirty.Dispose();
				_terrainJobs.RemoveAt(i);
			}
		}

		// ── Step 4: Schedule terrain jobs ─────────────────────────────────────

		private void ScheduleTerrainJobs(ref SystemState state, ref EntityManager em,
			ref WorldSettingsSingleton       settings,
			ref WorldBlockRegistrySingleton  registry)
		{
			if (_terrainJobs.Count >= GameSettings.MAX_CONCURRENT_JOBS) return;

			var query = SystemAPI.QueryBuilder()
				.WithAll<IsVisible, ChunkPositionComponent, ChunkComponent, ChunkPriorityComponent>()
				.WithNone<IsPopulated, MarkedToDestroy>()
				.Build();

			if (query.IsEmpty) return;

			var entities   = query.ToEntityArray(Allocator.Temp);
			var priorities = query.ToComponentDataArray<ChunkPriorityComponent>(Allocator.Temp);

			// Priority queue — process closest chunks first
			var pq = new NativePriorityQueue<Entity>(entities.Length, Allocator.Temp);
			for (int i = 0; i < entities.Length; i++)
			{
				Entity e     = entities[i];
				int3   coord = WorldToChunkCoord(em.GetComponentData<ChunkPositionComponent>(e).WorldPosition);

				if (_terrainReady.ContainsKey(coord)) continue; // already done terrain

				bool running = false;
				foreach (ActiveTerrainJob j in _terrainJobs)
				{
					if (j.Entity != e) continue;
					running = true;
					break;
				}
				if (!running) pq.Enqueue(e, priorities[i].Distance);
			}

			int slots = math.min(GameSettings.MAX_CONCURRENT_JOBS - _terrainJobs.Count, pq.Count);
			for (int i = 0; i < slots; i++)
			{
				Entity e        = pq.Dequeue();
				int3   worldPos = em.GetComponentData<ChunkPositionComponent>(e).WorldPosition;
				int3   coord    = WorldToChunkCoord(worldPos);

				var blockData = new NativeArray<BlockState>(
					VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE * VoxelData.CHUNK_SIZE,
					Allocator.Persistent);
				var isDirty = new NativeReference<bool>(Allocator.Persistent) { Value = false };

				// --- Pass A: terrain shape ---
				var terrainJob = new TerrainShapePassJob
				{
					BlockData            = blockData,
					BlockPrototypes      = registry.Blocks,
					Noise                = _noise,
					ChunkWorldPos        = worldPos,
					ChunkSize            = VoxelData.CHUNK_SIZE,
					BiomeHeight          = settings.BiomeHeightCurve,
					ErosionCurve         = settings.ErosionCurve,
					PeaksAndValleysCurve = settings.PeaksAndValleysCurve,
					Seed                 = settings.Seed,
					IsDirty              = isDirty,
				};
				JobHandle terrainHandle = terrainJob.ScheduleByRef();

				// --- Pass B: cave carving (depends on terrain) ---
				var cavesJob = new CavesPassJob
				{
					BlockData       = blockData,
					BlockPrototypes = registry.Blocks,
					ChunkWorldPos   = worldPos,
					ChunkSize       = VoxelData.CHUNK_SIZE,
					Seed            = settings.Seed,
					MaxCaveWorldY   = 60,
				};
				JobHandle cavesHandle = cavesJob.ScheduleByRef(terrainHandle);

				_terrainJobs.Add(new ActiveTerrainJob
				{
					Entity     = e,
					ChunkCoord = coord,
					Handle     = cavesHandle,
					BlockData  = blockData,
					IsDirty    = isDirty,
				});
			}

			if (slots > 0) JobHandle.ScheduleBatchedJobs();

			pq.Dispose();
			entities.Dispose();
			priorities.Dispose();
		}

		// ── Step 5: Schedule decoration batch ─────────────────────────────────

		private void ScheduleDecorationBatch(ref SystemState state, EntityManager em,
			ref WorldSettingsSingleton       settings,
			ref WorldBlockRegistrySingleton  registry)
		{
			if (_terrainReady.Count == 0) return;

			// Build a combined set of all coords that have finished terrain:
			//   (a) chunks still awaiting decoration  →  _terrainReady
			//   (b) fully decorated chunks            →  ECS IsPopulated
			// This prevents an already-decorated neighbour from blocking new chunks.
			var populatedQuery = SystemAPI.QueryBuilder()
				.WithAll<IsPopulated, ChunkPositionComponent>()
				.WithNone<MarkedToDestroy>()
				.Build();

			var popPos = populatedQuery.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);
			var allReady = new NativeHashSet<int3>(
				_terrainReady.Count + popPos.Length + 1, Allocator.Temp);

			foreach (var kvp in _terrainReady) allReady.Add(kvp.Key);
			foreach (var p in popPos)          allReady.Add(WorldToChunkCoord(p.WorldPosition));
			popPos.Dispose();

			// Collect _terrainReady chunks whose required neighbours are all ready
			var toDecorate = new NativeList<int3>(Allocator.Temp);
			foreach (var kvp in _terrainReady)
				if (AllNeighboursReady(kvp.Key, allReady)) toDecorate.Add(kvp.Key);

			if (toDecorate.Length == 0)
			{
				toDecorate.Dispose();
				allReady.Dispose();
				return;
			}

			// Build the shared block-data map for decoration jobs.
			// Includes both _terrainReady chunks and already-IsPopulated chunks so
			// cross-border tree canopies can reach any loaded neighbour.
			var popEntities  = populatedQuery.ToEntityArray(Allocator.Temp);
			var popPositions = populatedQuery.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			_chunkDataMap = new NativeParallelHashMap<int3, ChunkBlockDataRef>(
				_terrainReady.Count + popEntities.Length + 1, Allocator.Persistent);

			foreach (var kvp in _terrainReady)
				if (kvp.Value.BlockData.IsCreated)
					_chunkDataMap.TryAdd(kvp.Key, ChunkBlockDataRef.From(kvp.Value.BlockData));

			for (int i = 0; i < popEntities.Length; i++)
			{
				int3 coord = WorldToChunkCoord(popPositions[i].WorldPosition);
				if (_chunkDataMap.ContainsKey(coord)) continue; // already from _terrainReady
				var comp = em.GetComponentData<ChunkComponent>(popEntities[i]);
				if (comp.BlockData.IsCreated)
					_chunkDataMap.TryAdd(coord, ChunkBlockDataRef.From(comp.BlockData));
			}
			popEntities.Dispose();
			popPositions.Dispose();

			_dirtyMap = new NativeParallelHashMap<int3, bool>(toDecorate.Length * 9, Allocator.Persistent);
			_decorBatchCoords.Clear();

			var    dirtyWriter = _dirtyMap.AsParallelWriter();
			ushort airID       = registry.Blocks[0].ID;
			ushort grassID     = registry.Blocks[3].ID;
			ushort logID       = registry.Blocks[7].ID;
			ushort leavesID    = registry.Blocks[10].ID;

			// Bucket into 4 colour groups for checkerboard wave scheduling
			var groups = new NativeArray<NativeList<int3>>(4, Allocator.Temp);
			for (int c = 0; c < 4; c++)
				groups[c] = new NativeList<int3>(Allocator.Temp);

			for (int i = 0; i < toDecorate.Length; i++)
			{
				int3 coord  = toDecorate[i];
				int  colour = PositiveMod(coord.x, 2) * 2 + PositiveMod(coord.z, 2);
				groups[colour].Add(coord);
				_decorBatchCoords.Add(coord);
			}

			// Chain four waves: each wave depends on the previous wave's combined handle.
			// All jobs within a wave run in parallel (same-colour chunks never overlap).
			JobHandle waveHandle = default;
			for (int colour = 0; colour < 4; colour++)
			{
				NativeList<int3> group = groups[colour];
				if (group.Length == 0) continue;

				var waveHandles = new NativeArray<JobHandle>(group.Length, Allocator.Temp);
				for (int i = 0; i < group.Length; i++)
				{
					int3             coord = group[i];
					TerrainReadyEntry entry = _terrainReady[coord];

					var job = new DecorationPassJob
					{
						OwnBlockData   = entry.BlockData,
						OreTypes       = registry.OreTypes,
						ChunkDataMap   = _chunkDataMap,
						DirtyWriter    = dirtyWriter,
						ChunkWorldPos  = coord * VoxelData.CHUNK_SIZE,
						ChunkSize      = VoxelData.CHUNK_SIZE,
						Seed           = settings.Seed,
						AirID          = airID,
						GrassID        = grassID,
						LogID          = logID,
						LeavesID       = leavesID,
						TreeDensity    = registry.TreeDensity,
						MinTrunkHeight = registry.MinTrunkHeight,
						MaxTrunkHeight = registry.MaxTrunkHeight,
					};
					waveHandles[i] = job.ScheduleByRef(waveHandle);
				}

				waveHandle = JobHandle.CombineDependencies(waveHandles);
				waveHandles.Dispose();
			}

			for (int c = 0; c < 4; c++) groups[c].Dispose();
			groups.Dispose();
			toDecorate.Dispose();
			allReady.Dispose();

			_decorBatchHandle = waveHandle;
			_decorBatchActive = true;
			JobHandle.ScheduleBatchedJobs();
		}

		// ── Step 2: Apply decoration results ──────────────────────────────────

		private void ApplyDecorationResults(ref SystemState state)
		{
			EntityManager em = state.EntityManager;

			// Finalise every chunk decorated in this batch
			foreach (int3 coord in _decorBatchCoords)
			{
				if (!_terrainReady.TryGetValue(coord, out TerrainReadyEntry entry)) continue;

				bool finalDirty = entry.IsDirty || _dirtyMap.ContainsKey(coord);

				if (em.Exists(entry.Entity))
				{
					// Transfer block data ownership to ChunkComponent exactly once,
					// at the moment the chunk becomes fully generated.
					// This is the only place comp.BlockData is ever written,
					// preventing the double-dispose that occurs if it is set earlier.
					var comp = em.GetComponentData<ChunkComponent>(entry.Entity);
					comp.BlockData = entry.BlockData;
					em.SetComponentData(entry.Entity, comp);

					// *** IsPopulated is added HERE — after all three passes ***
					em.AddComponentData(entry.Entity, new IsPopulated());

					if (finalDirty) em.AddComponentData(entry.Entity, new NeedsMeshSync());
					else            em.AddComponentData(entry.Entity, new IsEmpty());
				}
				else
				{
					entry.BlockData.Dispose();
				}

				_terrainReady.Remove(coord);
			}

			// Propagate dirty flag to already-IsPopulated neighbours that received
			// decoration writes (e.g. tree canopy crossing a chunk border).
			var q = SystemAPI.QueryBuilder()
				.WithAll<IsPopulated, ChunkPositionComponent>()
				.WithNone<MarkedToDestroy>()
				.Build();

			var allEntities  = q.ToEntityArray(Allocator.Temp);
			var allPositions = q.ToComponentDataArray<ChunkPositionComponent>(Allocator.Temp);

			for (int i = 0; i < allEntities.Length; i++)
			{
				int3   coord = WorldToChunkCoord(allPositions[i].WorldPosition);
				Entity e     = allEntities[i];

				if (!_dirtyMap.ContainsKey(coord) || !em.Exists(e)) continue;
				if (em.HasComponent<IsEmpty>(e))        em.RemoveComponent<IsEmpty>(e);
				if (!em.HasComponent<NeedsMeshSync>(e)) em.AddComponentData(e, new NeedsMeshSync());
			}

			allEntities.Dispose();
			allPositions.Dispose();

			// Propagate dirty flag to _terrainReady neighbours that received writes
			// (they'll pick it up when they eventually get IsPopulated)
			foreach (var kvp in _dirtyMap)
			{
				int3 coord = kvp.Key;
				if (!_terrainReady.TryGetValue(coord, out TerrainReadyEntry entry)) continue;
				entry.IsDirty      = true;
				_terrainReady[coord] = entry;
			}
		}

		// ── Cleanup ───────────────────────────────────────────────────────────

		/// <summary>Dispose block data for any _terrainReady entry whose entity was destroyed.</summary>
		private void CleanupDestroyedEntries(EntityManager em)
		{
			using var keys = _terrainReady.GetKeyArray(Allocator.Temp);
			foreach (int3 key in keys)
			{
				TerrainReadyEntry entry = _terrainReady[key];
				if (em.Exists(entry.Entity)) continue;
				if (entry.BlockData.IsCreated) entry.BlockData.Dispose();
				_terrainReady.Remove(key);
			}
		}

		private void DisposeBatchResources()
		{
			if (_chunkDataMap.IsCreated) _chunkDataMap.Dispose();
			if (_dirtyMap.IsCreated)     _dirtyMap.Dispose();
		}

		// ── Noise init ────────────────────────────────────────────────────────

		private void InitNoise(ref WorldSettingsSingleton settings)
		{
			_noise     = FastNoise.FromEncodedNodeTree(settings.EncodedNodeTree.ToString());
			_noiseInit = true;
		}

		// ── Public API ────────────────────────────────────────────────────────

		/// <summary>
		/// Returns the <see cref="JobHandle"/> that must be completed before
		/// <paramref name="chunkEntity"/>'s block data can be safely read or written
		/// from the main thread or another job.
		///
		/// Three cases:
		///  • Chunk has an in-flight terrain+caves job  → return that job's handle.
		///  • A decoration batch is active              → return the batch handle.
		///    (Decoration jobs can write into ANY loaded chunk via cross-border tree
		///    canopies, so the whole batch must finish before any chunk is safe.)
		///  • No active work                            → return default (already safe).
		/// </summary>
		public JobHandle GetChunkDependency(Entity chunkEntity)
		{
			foreach (ActiveTerrainJob job in _terrainJobs)
				if (job.Entity == chunkEntity) return job.Handle;

			if (_decorBatchActive) return _decorBatchHandle;

			return default;
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		/// <summary>
		/// 8 horizontal neighbours + chunk above must all be terrain-ready before
		/// decoration.  Checks <paramref name="allReady"/> which combines both
		/// <see cref="_terrainReady"/> chunks and already-IsPopulated ECS chunks.
		/// </summary>
		private static bool AllNeighboursReady(int3 coord, in NativeHashSet<int3> allReady)
		{
			for (int dx = -1; dx <= 1; dx++)
			for (int dz = -1; dz <= 1; dz++)
			{
				if (dx == 0 && dz == 0) continue;
				if (!allReady.Contains(coord + new int3(dx, 0, dz))) return false;
			}
			return allReady.Contains(coord + new int3(0, 1, 0));
		}

		private static int3 WorldToChunkCoord(int3 world)
		{
			int s = VoxelData.CHUNK_SIZE;
			return new int3(FloorDiv(world.x, s), FloorDiv(world.y, s), FloorDiv(world.z, s));
		}

		private static int FloorDiv(int a, int b)
			=> a / b - (a % b != 0 && (a ^ b) < 0 ? 1 : 0);

		private static int PositiveMod(int a, int b) => ((a % b) + b) % b;

		// ── Inner types ───────────────────────────────────────────────────────

		private struct ActiveTerrainJob
		{
			public Entity                  Entity;
			public int3                    ChunkCoord;
			public JobHandle               Handle;
			public NativeArray<BlockState> BlockData;
			public NativeReference<bool>   IsDirty;
		}

		private struct TerrainReadyEntry
		{
			public Entity                  Entity;
			public NativeArray<BlockState> BlockData;
			/// <summary>True if terrain or any decoration pass placed a non-air block.</summary>
			public bool IsDirty;
		}
	}
}