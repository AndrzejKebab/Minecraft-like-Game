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
	///
	/// Fluid blocks (water, lava) are never carved so underground lakes and
	/// rivers remain intact and surface water is not broken.
	///
	/// Tuning knobs
	/// ────────────
	/// • <see cref="CaveFrequency"/>  — lower = bigger, wider tunnels
	/// • <see cref="CaveThreshold"/> — higher = fewer, smaller caves (0.05–0.25)
	/// • <see cref="MaxCaveWorldY"/> — no caves above this Y (prevents surface holes)
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct CavesPassJob : IJob
	{
		public            NativeArray<BlockState> BlockData;
		[ReadOnly] public NativeArray<Block>      BlockPrototypes;

		public int3   ChunkWorldPos;
		public int    ChunkSize;
		public int    Seed;
		public int    MaxCaveWorldY; // e.g. 60 — prevents caves from breaking through the surface

		public float CaveFrequency; // default 0.04
		public float CaveThreshold; // default 0.12

		public void Execute()
		{
			float  seedShift = Seed * 0.0013f;
			float  freq      = CaveFrequency > 0f ? CaveFrequency : 0.04f;
			float  threshold = CaveThreshold > 0f ? CaveThreshold : 0.12f;
			float3 offset    = new float3(31.7f, 17.3f, 53.1f);

			for (int z = 0; z < ChunkSize; z++)
			{
				float pz = (ChunkWorldPos.z + z) * freq + seedShift;

				for (int y = 0; y < ChunkSize; y++)
				{
					int worldY = ChunkWorldPos.y + y;
					if (worldY > MaxCaveWorldY) continue; // Early Skip
					float py = worldY * freq + seedShift;

					for (int x = 0; x < ChunkSize; x++)
					{
						int idx   = x | (y << 5) | (z << 10);
						var block = BlockData[idx];

						if (block.ID == 0) continue; // Skip air
						if (block.ID < BlockPrototypes.Length && BlockPrototypes[block.ID].IsFluid)
							continue; // Skip fluids

						float  px = (ChunkWorldPos.x + x) * freq + seedShift;
						float3 p  = new float3(px, py, pz);

						float n1 = noise.snoise(p);
						float n2 = noise.snoise(p + offset);

						if (n1 * n2 > threshold)
						{
							BlockData[idx] = new BlockState { ID = 0, Orientation = 0 };
						}
					}
				}
			}
		}
	}
}