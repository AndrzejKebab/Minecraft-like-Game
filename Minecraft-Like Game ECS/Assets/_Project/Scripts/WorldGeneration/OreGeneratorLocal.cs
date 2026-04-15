using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using _Project.WorldGeneration.Blocks;

namespace _Project.WorldGeneration
{
	/// <summary>
	/// Chunk-local ore generation.  Replaces OreGenerator's hashmap-based API.
	/// Writes directly into ownData NativeArray.  Vein radius ≤ 2 ensures all
	/// ore blocks land in own chunk — same constraint as original.
	/// </summary>
	[BurstCompile(OptimizeFor    = OptimizeFor.Performance,
	              FloatMode      = FloatMode.Fast,
	              FloatPrecision = FloatPrecision.Low)]
	public static class OreGeneratorLocal
	{
		[BurstCompile]
		public static void Generate(
			ref NativeArray<BlockState>  ownData,
			ref NativeArray<OreSettings> oreTypes,
			ref int3                     chunkWorldPos,
			int                          chunkSize,
			int                          seed)
		{
			for (int o = 0; o < oreTypes.Length; o++)
				GenerateOreType(ref ownData, ref oreTypes, o, ref chunkWorldPos, chunkSize, seed);
		}

		[BurstCompile]
		private static void GenerateOreType(
			ref NativeArray<BlockState>  ownData,
			ref NativeArray<OreSettings> oreTypes,
			int                          oreIdx,
			ref int3                     chunkWorldPos,
			int                          chunkSize,
			int                          seed)
		{
			OreSettings ore = oreTypes[oreIdx];

			int chunkMinY = chunkWorldPos.y;
			int chunkMaxY = chunkWorldPos.y + chunkSize - 1;
			int yMin      = math.max(ore.MinWorldY, chunkMinY);
			int yMax      = math.min(ore.MaxWorldY, chunkMaxY);
			if (yMin > yMax) return;

			var rng = Random.CreateFromIndex(VeinHash(ref chunkWorldPos, seed, oreIdx));

			for (int v = 0; v < ore.VeinsPerChunk; v++)
			{
				int cx       = chunkWorldPos.x + rng.NextInt(0, chunkSize);
				int cy       = rng.NextInt(yMin, yMax + 1);
				int cz       = chunkWorldPos.z + rng.NextInt(0, chunkSize);
				int veinSize = rng.NextInt(1, ore.MaxVeinSize + 1);

				for (int b = 0; b < veinSize; b++)
				{
					float3 offset = rng.NextFloat3(new float3(-ore.VeinRadius), new float3(ore.VeinRadius));

					int bx = math.clamp(cx + (int)math.round(offset.x),
					                    chunkWorldPos.x, chunkWorldPos.x + chunkSize - 1);
					int by = math.clamp(cy + (int)math.round(offset.y), yMin, yMax);
					int bz = math.clamp(cz + (int)math.round(offset.z),
					                    chunkWorldPos.z, chunkWorldPos.z + chunkSize - 1);

					int lx  = bx - chunkWorldPos.x;
					int ly  = by - chunkWorldPos.y;
					int lz  = bz - chunkWorldPos.z;
					int idx = Utility.FlattenIndex(lx, ly, lz);

					var existing = ownData[idx];
					if (existing.ID != ore.TargetBlockID) continue;
					ownData[idx] = new BlockState { ID = ore.BlockID, Orientation = 0 };
				}
			}
		}

		[BurstCompile]
		private static uint VeinHash(ref int3 pos, int seed, int oreIdx)
		{
			unchecked
			{
				uint h = (uint)(pos.x * 1376312589
				                ^ pos.y * 1664525
				                ^ pos.z * 22695477
				                ^ seed  * 1013904223
				                ^ oreIdx * 134775813);
				h ^= h >> 16;
				h *= 0x45d9f3b;
				h ^= h >> 16;
				return h;
			}
		}
	}
}