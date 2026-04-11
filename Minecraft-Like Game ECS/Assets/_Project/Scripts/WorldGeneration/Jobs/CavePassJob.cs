using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Jobs
{
	/// <summary>
	/// Pass B of the chunk population pipeline.
	/// Carves cave tunnels into the solid terrain produced by
	/// <see cref="TerrainShapePassJob"/> using a "cheese cave" technique:
	/// two offset 3D Simplex noise fields are multiplied together — blocks are
	/// carved wherever the product exceeds <see cref="CaveThreshold"/>.
	/// Multiplying two independent noise samples creates irregular blob shapes
	/// rather than the flat slabs a single field would produce.
	///
	/// Tuning knobs
	/// ────────────
	/// • <see cref="CaveFrequency"/>  — lower = bigger, wider tunnels
	/// • <see cref="CaveThreshold"/> — higher = fewer, smaller caves (0.05–0.25)
	/// • <see cref="MaxCaveWorldY"/> — no caves above this Y (prevents surface holes)
	/// </summary>
	[BurstCompile(OptimizeFor    = OptimizeFor.Performance,
	              FloatMode      = FloatMode.Fast,
	              FloatPrecision = FloatPrecision.Low)]
	public struct CavesPassJob : IJob
	{
		public NativeArray<BlockState> BlockData;

		public int3   ChunkWorldPos;
		public int    ChunkSize;
		public int    Seed;
		public ushort StoneID;       // Only report block ID for logging; carving replaces any non-air block
		public int    MaxCaveWorldY; // e.g. 60 — prevents caves from breaking through the surface

		// Exposed so callers can tune without recompiling; set reasonable defaults in the system.
		public float CaveFrequency;  // default 0.04
		public float CaveThreshold;  // default 0.12

		public void Execute()
		{
			// Apply seed as a constant world-space offset so every seed produces a
			// completely different cave layout without changing the noise algorithm.
			float seedShift = Seed * 0.0013f;

			float freq      = CaveFrequency  > 0f ? CaveFrequency  : 0.04f;
			float threshold = CaveThreshold  > 0f ? CaveThreshold  : 0.12f;

			for (int x = 0; x < ChunkSize; x++)
			for (int y = 0; y < ChunkSize; y++)
			for (int z = 0; z < ChunkSize; z++)
			{
				int worldY = ChunkWorldPos.y + y;
				if (worldY > MaxCaveWorldY) continue; // no caves near / above surface

				int idx   = x * ChunkSize * ChunkSize + y * ChunkSize + z;
				var block = BlockData[idx];
				if (block.ID == 0) continue; // already air — skip

				float3 p = new float3(
					ChunkWorldPos.x + x,
					worldY,
					ChunkWorldPos.z + z) * freq + seedShift;

				// Two noise samples shifted apart so they're statistically independent
				float n1 = noise.snoise(p);
				float n2 = noise.snoise(p + new float3(31.7f, 17.3f, 53.1f));

				// Carve where both fields are simultaneously high
				if (n1 * n2 > threshold)
					BlockData[idx] = new BlockState { ID = 0, Orientation = 0 };
			}
		}
	}
}