using _Project.WorldGeneration.Blocks;
using FastNoise2.Bindings;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     Deterministic per-chunk tree projection.  Replaces TreeGenerator for the
	///     fused populate pipeline.  No cross-chunk writes; each chunk independently
	///     projects trees rooted in itself AND in its 27-neighbour halo into its own
	///     BlockData, clipping blocks that fall outside.
	///     Determinism contract:
	///     ColumnHash(worldX, worldZ, seed) === TreeGenerator.ColumnHash.
	///     Same world column always produces same tree → identical output across
	///     regenerations.  Trees rooted in chunk N project into N-1, N+1, etc.
	///     independently and consistently.
	///     Surface detection:
	///     Uses halo heightmap directly (no BlockData scan).  Tree only spawns
	///     if classified surface block at groundY is grass (not sand/water).
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class TreeGeneratorDeterministic
	{
		public const ushort REPLACE_ANY = ushort.MaxValue;

		// Tree dimensions — used for halo radius calc.  If you change these,
		// update HALO_RADIUS in ChunkPopulateJob.
		public const int   HR_HORIZONTAL = 3; // canopy half-width
		public const float VR_VERTICAL   = 2.2f;
		public const int   CANOPY_UP     = 3; // canopy max +Y from centre
		public const int   CANOPY_DOWN   = 1;

		[BurstCompile]
		public static void ProjectHaloTreesIntoChunk(
			ref NativeArray<BlockState> ownData,
			ref NativeArray<int>        haloHeights, // (chunkSize*3)² flat
			ref FastNoise               caveNoise,
			ref int3                    chunkWorldPos,
			int                         chunkSize,
			int                         seed,
			float                       treeDensity,
			int                         minTrunkHeight,
			int                         maxTrunkHeight,
			ushort                      airID,
			ushort                      grassID,
			ushort                      logID,
			ushort                      leavesID)
		{
			var haloSize = chunkSize * 3;
			var originX  = chunkWorldPos.x - chunkSize;
			var originZ  = chunkWorldPos.z - chunkSize;

			var chunkMinX = chunkWorldPos.x;
			var chunkMinY = chunkWorldPos.y;
			var chunkMinZ = chunkWorldPos.z;
			var chunkMaxY = chunkWorldPos.y + chunkSize;

			// Y-range early-out: if no halo column's tree could intersect this chunk's Y,
			// skip the whole pass.  Tree top = groundY + maxTrunkHeight + 1 + CANOPY_UP.
			// Tree bottom = groundY + 1.
			// We don't actually iterate column Y — we just check per-column whether any
			// part of the tree touches this chunk.

			for (var hz = 0; hz < haloSize; hz++)
			for (var hx = 0; hx < haloSize; hx++)
			{
				var worldX  = originX + hx;
				var worldZ  = originZ + hz;
				var groundY = haloHeights[hx + hz * haloSize];

				// Surface must be grass.
				var surfaceID = NoiseGenerator.ClassifyVoxel(groundY, groundY);
				if (surfaceID != grassID) continue;

				// Deterministic per-column RNG — MUST match TreeGenerator.ColumnHash.
				var rng = Random.CreateFromIndex(ColumnHash(worldX, worldZ, seed));
				if (rng.NextFloat() > treeDensity) continue;

				// Cave check — skip if cave carved surface or trunk-base voxel.
				// Same threshold as CavesPassJob (caveMap > 0 = air).
				if (IsCaveCarved(ref caveNoise, worldX, groundY, worldZ, seed)) continue;
				if (IsCaveCarved(ref caveNoise, worldX, groundY + 1, worldZ, seed)) continue;

				var trunkHeight = rng.NextInt(minTrunkHeight, maxTrunkHeight + 1);

				var treeBottomY = groundY + 1;
				var treeTopY    = groundY + trunkHeight + 1 + CANOPY_UP;

				// Skip if tree doesn't intersect own chunk's Y range.
				if (treeTopY < chunkMinY || treeBottomY >= chunkMaxY) continue;

				// Skip if horizontally too far (canopy half-width = HR).
				if (worldX + HR_HORIZONTAL < chunkMinX) continue;
				if (worldX - HR_HORIZONTAL >= chunkMinX + chunkSize) continue;
				if (worldZ + HR_HORIZONTAL < chunkMinZ) continue;
				if (worldZ - HR_HORIZONTAL >= chunkMinZ + chunkSize) continue;

				ProjectTree(ref ownData, worldX, groundY, worldZ, trunkHeight,
				            ref chunkWorldPos, chunkSize, airID, logID, leavesID);
			}
		}

		[BurstCompile]
		private static void ProjectTree(
			ref NativeArray<BlockState> ownData,
			int                         rootX,         int    groundY, int rootZ, int trunkHeight,
			ref int3                    chunkWorldPos, int    chunkSize,
			ushort                      airID,         ushort logID, ushort leavesID)
		{
			// Trunk
			for (var i = 1; i <= trunkHeight; i++)
			{
				var wy         = groundY + i;
				var blockState = new BlockState { ID = logID, Orientation = 0 };
				WriteIfInside(ref ownData, rootX, wy, rootZ, ref chunkWorldPos, chunkSize,
				              ref blockState, REPLACE_ANY);
			}

			// Canopy — flattened ellipsoid one above trunk tip.
			var canopyCentreY = groundY + trunkHeight + 1;
			var vr            = HR_HORIZONTAL / VR_VERTICAL;

			for (var lz = -HR_HORIZONTAL; lz <= HR_HORIZONTAL; lz++)
			for (var lx = -HR_HORIZONTAL; lx <= HR_HORIZONTAL; lx++)
			for (var ly = -CANOPY_DOWN; ly <= CANOPY_UP; ly++)
			{
				var ev = ly * vr;
				var d  = math.sqrt(lx * lx + ev * ev + lz * lz);
				if (d > HR_HORIZONTAL) continue;
				if (lx == 0 && lz == 0 && ly <= 0) continue;

				var wx         = rootX + lx;
				var wy         = canopyCentreY + ly;
				var wz         = rootZ + lz;
				var blockState = new BlockState { ID = leavesID, Orientation = 0 };
				WriteIfInside(ref ownData, wx, wy, wz, ref chunkWorldPos, chunkSize,
				              ref blockState, airID);
			}
		}

		[BurstCompile]
		private static void WriteIfInside(
			ref NativeArray<BlockState> ownData,
			int                         wx,            int    wy, int wz,
			ref int3                    chunkWorldPos, int    chunkSize,
			ref BlockState              newState,      ushort requiredID)
		{
			var lx = wx - chunkWorldPos.x;
			var ly = wy - chunkWorldPos.y;
			var lz = wz - chunkWorldPos.z;
			if ((uint)lx >= (uint)chunkSize) return;
			if ((uint)ly >= (uint)chunkSize) return;
			if ((uint)lz >= (uint)chunkSize) return;

			var        idx      = Utility.FlattenIndex(lx, ly, lz);
			BlockState existing = ownData[idx];
			if (requiredID != REPLACE_ANY && existing.ID != requiredID) return;
			ownData[idx] = newState;
		}

		// Single-point cave noise sample. Burst-compatible.
		// FastNoise2 GenSingle3D returns scalar; threshold matches CavesPassJob.
		[BurstCompile]
		private static bool IsCaveCarved(
			ref FastNoise caveNoise, int wx, int wy, int wz, int seed)
		{
			var v = caveNoise.GenSingle3D(wx, wy, wz, seed);
			return v > 0f;
		}

		// MUST match TreeGenerator.ColumnHash for output determinism.
		[BurstCompile]
		private static uint ColumnHash(int x, int z, int seed)
		{
			unchecked
			{
				var h = (uint)((x * 1376312589) ^ (z * 1664525) ^ (seed * 22695477));
				h ^= h >> 16;
				h *= 0x45d9f3b;
				h ^= h >> 16;
				return h;
			}
		}
	}
}