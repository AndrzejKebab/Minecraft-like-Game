using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Jobs
{
	/// <summary>
	/// Pass C of the chunk population pipeline.
	/// Decorates one chunk with trees and ores by writing directly into the
	/// shared <see cref="ChunkDataMap"/> — including blocks in neighbouring
	/// chunks for cross-border tree canopies.
	///
	/// SAFETY CONTRACT
	/// ───────────────
	/// <see cref="ChunkDataMap"/> is annotated with
	/// <c>[NativeDisableContainerSafetyRestriction]</c>, which suppresses
	/// Unity's automatic aliasing checks.  This is intentional and safe
	/// PROVIDED <see cref="ChunkPopulateSystem"/> schedules jobs in
	/// checkerboard waves:
	///
	///   Colour = PositiveMod(coord.x, 2) * 2 + PositiveMod(coord.z, 2)   (0–3)
	///
	/// Two jobs of the same colour are always ≥2 chunks apart in X or Z.
	/// With a max tree radius of 3 blocks and chunk size ≥16, their write
	/// zones cannot overlap → no data race, no undefined behaviour.
	///
	/// <see cref="DirtyWriter"/> is a <c>ParallelWriter</c> on a
	/// <c>NativeParallelHashMap</c>, which IS thread-safe by design.
	/// </summary>
	[BurstCompile(OptimizeFor    = OptimizeFor.Performance,
	              FloatMode      = FloatMode.Fast,
	              FloatPrecision = FloatPrecision.Low)]
	public struct DecorationPassJob : IJob
	{
		// ── Inputs ────────────────────────────────────────────────────────────

		/// <summary>This chunk's terrain block data, used to locate grass surfaces.</summary>
		[ReadOnly] public NativeArray<BlockState> OwnBlockData;

		[ReadOnly] public NativeArray<OreSettings> OreTypes;

		/// <summary>
		/// All terrain-ready + already-populated chunks keyed by chunk coord.
		/// Safety restriction is disabled — see class doc for the guarantee that
		/// makes this safe when jobs are scheduled in checkerboard waves.
		/// </summary>
		[NativeDisableContainerSafetyRestriction]
		public NativeParallelHashMap<int3, ChunkBlockDataRef> ChunkDataMap;

		/// <summary>
		/// Thread-safe parallel writer — records which chunk coords received at
		/// least one block write so the system can trigger mesh sync.
		/// </summary>
		[NativeDisableContainerSafetyRestriction]
		public NativeParallelHashMap<int3, bool>.ParallelWriter DirtyWriter;

		// ── Chunk identity ────────────────────────────────────────────────────

		public int3 ChunkWorldPos;
		public int  ChunkSize;
		public int  Seed;

		// ── Block IDs (resolved from registry on the main thread before scheduling) ──

		public ushort AirID;
		public ushort GrassID;
		public ushort LogID;
		public ushort LeavesID;

		// ── Settings ──────────────────────────────────────────────────────────

		public float TreeDensity;
		public int   MinTrunkHeight;
		public int   MaxTrunkHeight;

		// ── IJob ──────────────────────────────────────────────────────────────

		public void Execute()
		{
			// Ores first — confined to own chunk at small radii, no cross-border writes
			OreGenerator.Generate(
				ref ChunkDataMap, ref DirtyWriter,
				ref OreTypes,
				ref ChunkWorldPos, ChunkSize, Seed);

			// Trees after — can spill into neighbours via ChunkDataMap
			TreeGenerator.Generate(
				ref OwnBlockData,
				ref ChunkDataMap,
				ref DirtyWriter,
				ref ChunkWorldPos, ChunkSize, Seed,
				TreeDensity, MinTrunkHeight, MaxTrunkHeight,
				AirID, GrassID, LogID, LeavesID);
		}
	}
}