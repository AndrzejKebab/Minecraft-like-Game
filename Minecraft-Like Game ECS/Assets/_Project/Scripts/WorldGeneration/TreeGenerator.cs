using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using _Project.WorldGeneration.Blocks;

namespace _Project.WorldGeneration
{
	/// <summary>
	/// Burst-compiled tree generation.
	/// Writes logs and leaves directly into the shared
	/// <c>NativeParallelHashMap&lt;int3, ChunkBlockDataRef&gt;</c>
	/// so trees that cross chunk borders are handled without any intermediate
	/// VoxelMod buffer or main-thread routing pass.
	///
	/// THREAD SAFETY
	/// ─────────────
	/// Safe to call from concurrent jobs ONLY when the calling jobs are in the
	/// same checkerboard colour group (guaranteed ≥2 chunks apart in XZ),
	/// which ensures their write zones never overlap.
	///
	/// SURFACE DETECTION
	/// ─────────────────
	/// <see cref="FindSurface"/> searches specifically for a grass block rather
	/// than the first non-air block.  This prevents canopy leaves written into
	/// a neighbour by an earlier checkerboard wave from being mistaken for the
	/// ground surface, which would cause tree trunks to sprout mid-air above
	/// the existing canopy.
	/// </summary>
	[BurstCompile(OptimizeFor    = OptimizeFor.Performance,
	              FloatMode      = FloatMode.Fast,
	              FloatPrecision = FloatPrecision.Low)]
	public static class TreeGenerator
	{
		[BurstCompile]
		public static void Generate(
			ref  NativeArray<BlockState>                               ownBlockData,
			ref NativeParallelHashMap<int3, ChunkBlockDataRef>        chunkMap,
			ref NativeParallelHashMap<int3, bool>.ParallelWriter      dirtyWriter,
			ref int3   chunkWorldPos,
			int    chunkSize,
			int    seed,
			float  treeDensity,
			int    minTrunkHeight,
			int    maxTrunkHeight,
			ushort airID,
			ushort grassID,
			ushort logID,
			ushort leavesID)
		{
			for (var x = 0; x < chunkSize; x++)
			for (var z = 0; z < chunkSize; z++)
			{
				// Find the highest GRASS block in this column.
				// Searching for grassID (not just any non-air block) prevents
				// leaf canopies written by earlier decoration waves from being
				// misidentified as the surface, which would place tree roots
				// on top of existing canopies in a neighbouring chunk.
				int localSurface = FindSurface(ref ownBlockData, x, z, chunkSize, grassID);
				if (localSurface < 0) continue;

				int worldX = chunkWorldPos.x + x;
				int worldZ = chunkWorldPos.z + z;
				int worldY = chunkWorldPos.y + localSurface;

				// Deterministic per-column hash — same world pos always produces
				// the same tree regardless of which chunk iteration triggered it
				var rng = Random.CreateFromIndex(ColumnHash(worldX, worldZ, seed));

				if (rng.NextFloat() > treeDensity) continue;

				int trunkHeight = rng.NextInt(minTrunkHeight, maxTrunkHeight + 1);

				var root = new int3(worldX, worldY, worldZ);
				PlaceTree(ref chunkMap, ref dirtyWriter,
				          ref root,
				          trunkHeight, chunkSize,
				          airID, logID, leavesID);
			}
		}

		// ─────────────────────────────────────────────────────────────────────

		[BurstCompile]
		private static void PlaceTree(
			ref NativeParallelHashMap<int3, ChunkBlockDataRef>      chunkMap,
			ref NativeParallelHashMap<int3, bool>.ParallelWriter    dirtyWriter,
			ref int3   root,         // World-space position of the grass block
			int    trunkHeight,
			int    chunkSize,
			ushort airID,
			ushort logID,
			ushort leavesID)
		{
			// ── Trunk ─────────────────────────────────────────────────────────
			for (int i = 1; i <= trunkHeight; i++)
			{
				int3 worldPos   = root + new int3(0, i, 0);
				var  blockState = new BlockState { ID = logID, Orientation = 0 };
				WriteBlock(ref chunkMap, ref dirtyWriter,
				           ref worldPos, chunkSize,
				           ref blockState,
				           ChunkBlockDataRef.REPLACE_ANY);
			}

			// ── Canopy — flattened ellipsoid centred one above trunk tip ──────
			int canopyCentreY = trunkHeight + 1;
			const int   HR = 3;    // horizontal radius in blocks
			const float VR = 2.2f; // vertical scale (< HR → flat disc shape)

			for (int lx = -HR; lx <= HR; lx++)
			for (int lz = -HR; lz <= HR; lz++)
			for (int ly = -1;  ly <= HR; ly++) // asymmetric: more up than down
			{
				float d = math.sqrt(lx * lx
				                  + (ly * (HR / VR)) * (ly * (HR / VR))
				                  + lz * lz);
				if (d > HR) continue;

				// Don't cap the trunk with leaves
				if (lx == 0 && lz == 0 && ly <= 0) continue;

				// Leaves only replace air — won't carve into adjacent terrain
				int3 worldPos   = root + new int3(lx, canopyCentreY + ly, lz);
				var  blockState = new BlockState { ID = leavesID, Orientation = 0 };
				WriteBlock(ref chunkMap, ref dirtyWriter,
				           ref worldPos, chunkSize,
				           ref blockState,
				           airID);
			}
		}

		// ── Write helper ──────────────────────────────────────────────────────

		/// <summary>
		/// Resolves world position → chunk coord → flat index, then conditionally
		/// writes the block and marks the chunk dirty.
		/// Handles positions in any loaded chunk (cross-chunk trees just work).
		/// </summary>
		[BurstCompile]
		private static void WriteBlock(
			ref NativeParallelHashMap<int3, ChunkBlockDataRef>      chunkMap,
			ref NativeParallelHashMap<int3, bool>.ParallelWriter    dirtyWriter,
			ref int3       worldPos,
			int        chunkSize,
			ref BlockState value,
			ushort     requiredExistingID)
		{
			int3 coord = WorldToChunkCoord(worldPos, chunkSize);

			if (!chunkMap.TryGetValue(coord, out ChunkBlockDataRef chunkRef)) return;

			int3 local = worldPos - coord * chunkSize;
			if (math.any(local < 0) || math.any(local >= chunkSize)) return;

			if (chunkRef.TryWrite(ToIndex(local.x, local.y, local.z, chunkSize),
			                      value, requiredExistingID))
			{
				dirtyWriter.TryAdd(coord, true);
			}
		}

		// ── Utilities ─────────────────────────────────────────────────────────

		/// <summary>
		/// Finds the highest block with <paramref name="grassID"/> in column (x, z).
		/// Returns the local Y, or -1 if no grass block is found.
		///
		/// Searching for grassID specifically (rather than the first non-air block)
		/// prevents canopy leaves placed by an earlier checkerboard wave from
		/// being treated as the surface in neighbouring chunks.
		/// </summary>
		[BurstCompile]
		private static int FindSurface(
			ref NativeArray<BlockState> data, int x, int z, int size, ushort grassID)
		{
			for (int y = size - 1; y >= 0; y--)
				if (data[ToIndex(x, y, z, size)].ID == grassID) return y;
			return -1;
		}

		/// <summary>
		/// Must match the <c>SetAtIndex(x,y,z)</c> extension used in
		/// <see cref="TerrainShapePassJob"/>.  Change here if your layout differs.
		/// </summary>
		[BurstCompile]
		internal static int ToIndex(int x, int y, int z, int size)
			=> x * size * size + y * size + z;

		internal static int3 WorldToChunkCoord(int3 world, int size) =>
			new(FloorDiv(world.x, size),
			    FloorDiv(world.y, size),
			    FloorDiv(world.z, size));

		[BurstCompile]
		private static int FloorDiv(int a, int b)
			=> a / b - (a % b != 0 && (a ^ b) < 0 ? 1 : 0);

		[BurstCompile]
		private static uint ColumnHash(int x, int z, int seed)
		{
			unchecked
			{
				uint h = (uint)(x * 1376312589 ^ z * 1664525 ^ seed * 22695477);
				h ^= h >> 16;
				h *= 0x45d9f3b;
				h ^= h >> 16;
				return h;
			}
		}
	}
}