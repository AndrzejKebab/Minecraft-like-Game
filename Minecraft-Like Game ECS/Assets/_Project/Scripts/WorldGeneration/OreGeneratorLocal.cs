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

	
	/// <summary>
	///     Chunk-local ore generation.  Replaces OreGenerator's hashmap-based API.
	///     Writes directly into ownData NativeArray.  Vein radius ≤ 2 ensures all
	///     ore blocks land in own chunk — same constraint as original.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast,
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
			for (var o = 0; o < oreTypes.Length; o++)
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

			var chunkMinY = chunkWorldPos.y;
			var chunkMaxY = chunkWorldPos.y + chunkSize - 1;
			var yMin      = math.max(ore.MinWorldY, chunkMinY);
			var yMax      = math.min(ore.MaxWorldY, chunkMaxY);
			if (yMin > yMax) return;

			var rng = Random.CreateFromIndex(VeinHash(ref chunkWorldPos, seed, oreIdx));

			for (var v = 0; v < ore.VeinsPerChunk; v++)
			{
				var cx       = chunkWorldPos.x + rng.NextInt(0, chunkSize);
				var cy       = rng.NextInt(yMin, yMax + 1);
				var cz       = chunkWorldPos.z + rng.NextInt(0, chunkSize);
				var veinSize = rng.NextInt(1, ore.MaxVeinSize + 1);

				for (var b = 0; b < veinSize; b++)
				{
					float3 offset = rng.NextFloat3(new float3(-ore.VeinRadius), new float3(ore.VeinRadius));

					var bx = math.clamp(cx + (int)math.round(offset.x),
					                    chunkWorldPos.x, chunkWorldPos.x + chunkSize - 1);
					var by = math.clamp(cy + (int)math.round(offset.y), yMin, yMax);
					var bz = math.clamp(cz + (int)math.round(offset.z),
					                    chunkWorldPos.z, chunkWorldPos.z + chunkSize - 1);

					var lx  = bx - chunkWorldPos.x;
					var ly  = by - chunkWorldPos.y;
					var lz  = bz - chunkWorldPos.z;
					var idx = Utility.FlattenIndex(lx, ly, lz);

					BlockState existing = ownData[idx];
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