using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.TerraGen;
using FastNoise2.Bindings;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     Deterministic per-chunk tree projection for the fused TerraGen populate
	///     pipeline. No cross-chunk writes: each chunk independently projects every tree
	///     rooted within canopy reach (own columns + a <see cref="HR_HORIZONTAL" />-block
	///     fringe) into its own BlockData, clipping blocks that fall outside.
	///
	///     Determinism contract: a tree is a pure function of its root column —
	///     ColumnHash(worldX, worldZ, seed) seeds the RNG, and the root's ground height,
	///     surface type and water state come from the shared TerraGen tile columns (the
	///     same data every chunk of that column reads). So chunk N and its neighbours
	///     all compute the identical tree and each writes only its own slice of it —
	///     trunks and canopies span chunk borders, both horizontally and vertically,
	///     with no cross-chunk communication.
	///
	///     Trees spawn only on dry grass surfaces (TerraGenerator.ClassifyVoxel at the
	///     column surface), never on sand/stone/underwater, and skip columns whose
	///     surface or trunk base the cave noise carves away (same threshold as the cave
	///     pass, sampled pointwise).
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class TreeGeneratorDeterministic
	{
		public const ushort REPLACE_ANY = ushort.MaxValue;

		// Tree dimensions. HR_HORIZONTAL bounds the fringe of neighbour columns a chunk
		// must consider; CANOPY_UP feeds the populate job's above-terrain fast-path
		// headroom so canopies aren't clipped at vertical chunk borders.
		public const int   HR_HORIZONTAL = 3; // canopy half-width
		public const float VR_VERTICAL   = 2.2f;
		public const int   CANOPY_UP     = 3; // canopy max +Y from centre
		public const int   CANOPY_DOWN   = 1;

		/// <summary>
		///     Project every tree rooted in this chunk's columns or the surrounding
		///     canopy fringe into <paramref name="ownData" />. Root data comes straight
		///     from the tile slice (SurfaceY / WaterY / terrain classification), which the
		///     tile border guarantees is available for the whole fringe.
		/// </summary>
		public static unsafe void ProjectTileTreesIntoChunk(
			ref NativeArray<BlockState> ownData,
			in TerraTileSlice           slice,
			ref FastNoise               caveNoise,
			int3                        chunkWorldPos,
			int                         chunkSize,
			int                         stoneLineY,
			int                         seed,
			float                       treeDensity,
			int                         minTrunkHeight,
			int                         maxTrunkHeight,
			ushort                      airID,
			ushort                      logID,
			ushort                      leavesID)
		{
			var gen       = TerraTileConst.GEN_BLOCKS;
			var chunkMinY = chunkWorldPos.y;
			var chunkMaxY = chunkWorldPos.y + chunkSize;

			// Only columns whose canopy can reach this chunk: own 32×32 plus HR fringe.
			for (var wz = chunkWorldPos.z - HR_HORIZONTAL; wz < chunkWorldPos.z + chunkSize + HR_HORIZONTAL; wz++)
			for (var wx = chunkWorldPos.x - HR_HORIZONTAL; wx < chunkWorldPos.x + chunkSize + HR_HORIZONTAL; wx++)
			{
				var         ci     = wx - slice.OriginX + (wz - slice.OriginZ) * gen;
				TerraColumn column = slice.Columns[ci];

				// dry land only — no trees in seas, rivers or on their beds
				if (column.WaterY > column.SurfaceY) continue;

				// surface must classify as grass (not sand/beach/desert/stone/mountain-top)
				if (TerraGenerator.ClassifyVoxel(column.SurfaceY, in column, stoneLineY) != TerraGenerator.GRASS)
					continue;

				// deterministic per-column RNG — same column, same tree, in every chunk
				var rng = Random.CreateFromIndex(ColumnHash(wx, wz, seed));
				if (rng.NextFloat() > treeDensity) continue;

				var groundY = column.SurfaceY;

				// skip if the cave noise carves the surface or the trunk base away
				// (same threshold as the populate cave pass, sampled pointwise)
				if (IsCaveCarved(ref caveNoise, wx, groundY, wz, seed)) continue;
				if (IsCaveCarved(ref caveNoise, wx, groundY + 1, wz, seed)) continue;

				var trunkHeight = rng.NextInt(minTrunkHeight, maxTrunkHeight + 1);

				var treeBottomY = groundY + 1;
				var treeTopY    = groundY + trunkHeight + 1 + CANOPY_UP;

				// skip if no part of the tree intersects this chunk's Y range
				if (treeTopY < chunkMinY || treeBottomY >= chunkMaxY) continue;

				ProjectTree(ref ownData, wx, groundY, wz, trunkHeight,
				            chunkWorldPos, chunkSize, airID, logID, leavesID);
			}
		}

		private static void ProjectTree(
			ref NativeArray<BlockState> ownData,
			int                         rootX,         int    groundY, int rootZ, int trunkHeight,
			int3                        chunkWorldPos, int    chunkSize,
			ushort                      airID,         ushort logID, ushort leavesID)
		{
			// Trunk — replaces anything (grass tufts, leaves of an older neighbour tree).
			for (var i = 1; i <= trunkHeight; i++)
			{
				var wy         = groundY + i;
				var blockState = new BlockState { ID = logID, Orientation = 0 };
				WriteIfInside(ref ownData, rootX, wy, rootZ, chunkWorldPos, chunkSize,
				              blockState, REPLACE_ANY);
			}

			// Canopy — flattened ellipsoid one above trunk tip; fills air only, so it
			// never eats terrain, water or another tree's trunk.
			var canopyCentreY = groundY + trunkHeight + 1;
			var vr            = HR_HORIZONTAL / VR_VERTICAL;

			for (var lz = -HR_HORIZONTAL; lz <= HR_HORIZONTAL; lz++)
			for (var lx = -HR_HORIZONTAL; lx <= HR_HORIZONTAL; lx++)
			for (var ly = -CANOPY_DOWN; ly <= CANOPY_UP; ly++)
			{
				var ev = ly * vr;
				var d  = math.sqrt(lx * lx + ev * ev + lz * lz);
				if (d > HR_HORIZONTAL) continue;
				if (lx == 0 && lz == 0 && ly <= 0) continue; // trunk occupies the core

				var wx         = rootX + lx;
				var wy         = canopyCentreY + ly;
				var wz         = rootZ + lz;
				var blockState = new BlockState { ID = leavesID, Orientation = 0 };
				WriteIfInside(ref ownData, wx, wy, wz, chunkWorldPos, chunkSize,
				              blockState, airID);
			}
		}

		private static void WriteIfInside(
			ref NativeArray<BlockState> ownData,
			int                         wx,            int wy, int wz,
			int3                        chunkWorldPos, int chunkSize,
			BlockState                  newState,      ushort requiredID)
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

		// Single-point cave noise sample; threshold matches the populate cave pass
		// (caveMap > 0 = carved to air).
		private static bool IsCaveCarved(
			ref FastNoise caveNoise, int wx, int wy, int wz, int seed)
		{
			var v = caveNoise.GenSingle3D(wx, wy, wz, seed);
			return v > 0f;
		}

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
