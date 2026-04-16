using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	// ── Data ─────────────────────────────────────────────────────────────────

	/// <summary>Blittable ore configuration.  Store in a NativeArray.</summary>
	public struct OreSettings
	{
		public ushort BlockID;       // Block to place
		public ushort TargetBlockID; // Only replace this block (e.g. stone)
		public int    MinWorldY;
		public int    MaxWorldY;
		public int    VeinsPerChunk;
		public int    MaxVeinSize;
		public float  VeinRadius; // Keep ≤ 2 to stay within the chunk
	}

// ── Generator ────────────────────────────────────────────────────────────

	/// <summary>
	///     Burst-compiled ore vein generation.
	///     Like <see cref="TreeGenerator" />, writes directly into the shared chunk
	///     map — no intermediate buffers needed.
	///     Vein radii are intentionally kept small (≤ 2) so individual ore blocks
	///     stay within the owning chunk, meaning ores are safe to generate in
	///     parallel without the checkerboard constraint (they don't cross borders).
	///     If you ever increase <see cref="OreSettings.VeinRadius" /> beyond ~6,
	///     apply the same checkerboard scheduling used for trees.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class OreGenerator
	{
		[BurstCompile]
		public static void Generate(
			ref NativeParallelHashMap<int3, ChunkBlockDataRef>   chunkMap,
			ref NativeParallelHashMap<int3, bool>.ParallelWriter dirtyWriter,
			ref NativeArray<OreSettings>                         oreTypes,
			ref int3                                             chunkWorldPos,
			int                                                  chunkSize,
			int                                                  seed)
		{
			for (var o = 0; o < oreTypes.Length; o++)
				GenerateOreType(ref chunkMap, ref dirtyWriter, ref oreTypes,
				                o, ref chunkWorldPos, chunkSize, seed);
		}

		[BurstCompile]
		private static void GenerateOreType(
			ref NativeParallelHashMap<int3, ChunkBlockDataRef>   chunkMap,
			ref NativeParallelHashMap<int3, bool>.ParallelWriter dirtyWriter,
			ref NativeArray<OreSettings>                         oreTypes,
			int                                                  oreIdx,
			ref int3                                             chunkWorldPos,
			int                                                  chunkSize,
			int                                                  seed)
		{
			OreSettings ore = oreTypes[oreIdx];

			// Clamp ore Y range to this chunk's extents
			var chunkMinY = chunkWorldPos.y;
			var chunkMaxY = chunkWorldPos.y + chunkSize - 1;
			var yMin      = math.max(ore.MinWorldY, chunkMinY);
			var yMax      = math.min(ore.MaxWorldY, chunkMaxY);
			if (yMin > yMax) return;

			// Unique seed per chunk + ore type for independent vein patterns
			var rng = Random.CreateFromIndex(VeinHash(ref chunkWorldPos, seed, oreIdx));

			int3 chunkCoord = TreeGenerator.WorldToChunkCoord(chunkWorldPos, chunkSize);
			// Ore stays within this chunk — look it up once
			if (!chunkMap.TryGetValue(chunkCoord, out ChunkBlockDataRef chunkRef)) return;

			for (var v = 0; v < ore.VeinsPerChunk; v++)
			{
				// World-space vein centre, clamped to this chunk's XZ + ore Y range
				var cx = chunkWorldPos.x + rng.NextInt(0, chunkSize);
				var cy = rng.NextInt(yMin, yMax + 1);
				var cz = chunkWorldPos.z + rng.NextInt(0, chunkSize);

				var veinSize    = rng.NextInt(1, ore.MaxVeinSize + 1);
				var veinDirtied = false;

				for (var b = 0; b < veinSize; b++)
				{
					float3 offset = rng.NextFloat3(
					                               new float3(-ore.VeinRadius),
					                               new float3(ore.VeinRadius));

					// Clamp each ore block to remain in this chunk
					var bx = math.clamp(cx + (int)math.round(offset.x),
					                    chunkWorldPos.x, chunkWorldPos.x + chunkSize - 1);
					var by = math.clamp(cy + (int)math.round(offset.y), yMin, yMax);
					var bz = math.clamp(cz + (int)math.round(offset.z),
					                    chunkWorldPos.z, chunkWorldPos.z + chunkSize - 1);

					int3 local = new int3(bx, by, bz) - chunkWorldPos;

					if (chunkRef.TryWrite(Utility.FlattenIndex(local.x, local.y, local.z),
					                      new BlockState { ID = ore.BlockID, Orientation = 0 },
					                      ore.TargetBlockID))
						veinDirtied = true;
				}

				if (veinDirtied)
					dirtyWriter.TryAdd(chunkCoord, true);
			}
		}

		[BurstCompile]
		private static uint VeinHash(ref int3 pos, int seed, int oreIdx)
		{
			unchecked
			{
				var h = (uint)((pos.x * 1376312589)
				               ^ (pos.y * 1664525)
				               ^ (pos.z * 22695477)
				               ^ (seed * 1013904223)
				               ^ (oreIdx * 134775813));
				h ^= h >> 16;
				h *= 0x45d9f3b;
				h ^= h >> 16;
				return h;
			}
		}
	}
}