using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.TerraGen;
using FastNoise2.Bindings;
using NativeTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Jobs
{
	/// <summary>
	///     Single fused population job.  Per chunk:
	///     1. Read TerraGen columns from the cached tile (TerraTileSystem) — the
	///        ReTerraForged pipeline + erosion filters ran once per tile, not per chunk
	///     2. Terrain blocks (own 32³) with all-air / all-stone fast paths
	///     3. Caves (own 32³, FastNoise2)
	///     4. Ores (own chunk only)
	///     5. Trees (project from tile columns into own chunk)
	///     6. Tag IsPopulated + NeedsMeshSync (or IsEmpty)
	///     Fully deterministic per chunk.  No neighbor BlockData reads or writes.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct ChunkPopulateJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<Entity>                 Entities;
		[ReadOnly] public NativeArray<ChunkPositionComponent> Positions;

		[NativeDisableContainerSafetyRestriction]
		[ReadOnly] public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;

		/// <summary>
		///     Per-chunk view into the cached tile columns (parallel to Entities).
		///     Slices hold raw pointers into arrays owned by TerraTileCacheSingleton;
		///     this job is a registered reader via the tile's ReadHandle.
		/// </summary>
		[ReadOnly] public NativeArray<TerraTileSlice> TileSlices;

		[ReadOnly] public NativeArray<Block>       BlockPrototypes;
		[ReadOnly] public NativeArray<OreSettings> OreTypes;

		[ReadOnly] public FastNoise CavesNoise;

		public TerraGenSettings TerraSettings;

		public EntityCommandBuffer.ParallelWriter ECB;

		public int Seed;
		public int ChunkSize;

		public ushort AirID;
		public ushort GrassID;
		public ushort LogID;
		public ushort LeavesID;

		public float TreeDensity;
		public int   MinTrunkHeight;
		public int   MaxTrunkHeight;

		// solid blocks kept under a water column so caves can't undermine it
		private const int CAVE_WATER_SEAL = 5;

		// true when column c holds water at world height y (water fills SurfaceY+1..WaterY)
		private static bool WaterAt(in TerraColumn c, int y)
		{
			return y > c.SurfaceY && y <= c.WaterY;
		}

		public unsafe void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			// ── 1. Tile columns (generated + eroded once per tile) ─────────────
			TerraTileSlice slice   = TileSlices[index];
			var            gen     = TerraTileConst.GEN_BLOCKS;
			var            baseX   = chunkWorldPos.x - slice.OriginX;
			var            baseZ   = chunkWorldPos.z - slice.OriginZ;

			// mountain surfaces above this world Y turn to bare stone (also gates
			// tree placement — trees only root on grass surfaces)
			var stoneLineY = (int)(TerraSettings.MountainHeight * 0.55f);

			// vertical bounds of this chunk's own 32×32 columns PLUS the tree-canopy
			// fringe — a tree rooted up to HR blocks outside can reach into this chunk
			const int fringe     = TreeGeneratorDeterministic.HR_HORIZONTAL;
			var       minSurface = int.MaxValue;
			var       maxTop     = int.MinValue;
			for (var z = -fringe; z < ChunkSize + fringe; z++)
			for (var x = -fringe; x < ChunkSize + fringe; x++)
			{
				TerraColumn column = slice.Columns[baseX + x + (baseZ + z) * gen];
				minSurface = math.min(minSurface, column.SurfaceY);
				maxTop     = math.max(maxTop, math.max(column.SurfaceY, column.WaterY));
			}

			var worldYMin = chunkWorldPos.y;
			var worldYMax = chunkWorldPos.y + ChunkSize - 1;

			// ── fast path: fully above terrain, water AND any tree that could poke
			//    up into this chunk → empty chunk ────────────────────────────────
			var treeHeadroom = MaxTrunkHeight + 1 + TreeGeneratorDeterministic.CANOPY_UP;
			if (worldYMin > maxTop + treeHeadroom)
			{
				var air = new BlockState { ID = 0, Orientation = 0 };
				for (var i = 0; i < blockData.Length; i++) blockData[i] = air;

				ECB.RemoveComponent<NeedsPopulation>(index, entity);
				ECB.AddComponent<IsPopulated>(index, entity);
				ECB.AddComponent<IsEmpty>(index, entity);
				ECB.AddComponent(index, entity,
				                 new ChunkOcclusion { Mask = ChunkVisibility.AllFacesConnected });
				return;
			}

			// Above every surface and water column, but within tree headroom: only
			// trunk tops / canopies can land here — skip terrain, caves and ores.
			var treesOnly = worldYMin > maxTop;

			// ── 2. Terrain ─────────────────────────────────────────────────────
			if (treesOnly)
			{
				var air = new BlockState { ID = 0, Orientation = 0 };
				for (var i = 0; i < blockData.Length; i++) blockData[i] = air;
			}
			else if (worldYMax < minSurface - 4)
			{
				// fast path: fully below every surface layer → solid stone
				var stone = new BlockState { ID = TerraGenerator.STONE, Orientation = 0 };
				for (var i = 0; i < blockData.Length; i++) blockData[i] = stone;
			}
			else
			{
				for (var z = 0; z < ChunkSize; z++)
				for (var x = 0; x < ChunkSize; x++)
				{
					TerraColumn column = slice.Columns[baseX + x + (baseZ + z) * gen];

					for (var y = 0; y < ChunkSize; y++)
					{
						var worldY = chunkWorldPos.y + y;
						var id     = TerraGenerator.ClassifyVoxel(worldY, in column, stoneLineY);
						var idx    = x | (y << 5) | (z << 10);
						blockData[idx] = new BlockState { ID = id, Orientation = 0 };
					}
				}
			}

			// ── 3. Caves + 4. Ores (nothing to carve/seed in a trees-only chunk) ──
			if (!treesOnly)
			{
				NoiseGenerator.GenerateCaveMap(out NativeTexture3D<float> caveMap,
				                               ref CavesNoise, ref chunkWorldPos, ChunkSize, Seed);

				for (var z = 0; z < ChunkSize; z++)
				for (var x = 0; x < ChunkSize; x++)
				{
					var         ci     = baseX + x + (baseZ + z) * gen;
					TerraColumn column = slice.Columns[ci];

					// neighbour columns (the tile border guarantees these are in range)
					TerraColumn nXm = slice.Columns[ci - 1];
					TerraColumn nXp = slice.Columns[ci + 1];
					TerraColumn nZm = slice.Columns[ci - gen];
					TerraColumn nZp = slice.Columns[ci + gen];

					// keep a solid seal under any water column so a cave can't hollow out
					// its floor and leave the water floating (SurfaceY down to
					// SurfaceY-CAVE_WATER_SEAL+1); caves still hollow out everything deeper.
					var hasWater   = column.WaterY > column.SurfaceY;
					var sealDownTo = column.SurfaceY - CAVE_WATER_SEAL;

					for (var y = 0; y < ChunkSize; y++)
					{
						var        idx   = x | (y << 5) | (z << 10);
						BlockState block = blockData[idx];
						if (block.ID == 0) continue;
						if (block.ID < BlockPrototypes.Length && BlockPrototypes[block.ID].IsFluid) continue;

						var worldY = chunkWorldPos.y + y;

						// under our own water: keep the bed + a few blocks solid
						if (hasWater && worldY > sealDownTo) continue;

						// beside a neighbour's water: keep this block solid so the water
						// isn't left with an open (air) side face into the cave
						if (WaterAt(in nXm, worldY) || WaterAt(in nXp, worldY) ||
						    WaterAt(in nZm, worldY) || WaterAt(in nZp, worldY)) continue;

						if (caveMap[new int3(x, y, z)] > 0)
							blockData[idx] = new BlockState { ID = 0, Orientation = 0 };
					}
				}

				caveMap.Dispose();

				OreGeneratorLocal.Generate(ref blockData, ref OreTypes, ref chunkWorldPos, ChunkSize, Seed);
			}

			// ── 5. Trees (deterministic tile projection) ───────────────────────
			//      Each chunk projects every tree rooted in its columns or the canopy
			//      fringe straight from the tile columns; the tile border guarantees the
			//      fringe data exists, and the per-column hash guarantees neighbours
			//      compute the identical tree — so trees span chunk borders seamlessly.
			TreeGeneratorDeterministic.ProjectTileTreesIntoChunk(
			                                                     ref blockData, in slice, ref CavesNoise,
			                                                     chunkWorldPos, ChunkSize, stoneLineY, Seed,
			                                                     TreeDensity, MinTrunkHeight, MaxTrunkHeight,
			                                                     AirID, LogID, LeavesID);

			// ── 6. Emptiness check + tags ──────────────────────────────────────
			var hasBlocks = false;
			for (var i = 0; i < blockData.Length; i++)
				if (blockData[i].ID != 0)
				{
					hasBlocks = true;
					break;
				}

			ECB.RemoveComponent<NeedsPopulation>(index, entity);
			ECB.AddComponent<IsPopulated>(index, entity);

			if (hasBlocks) ECB.AddComponent<NeedsMeshSync>(index, entity);
			else ECB.AddComponent<IsEmpty>(index, entity);

			// Occlusion visibility mask straight from the fresh BlockData (needs no
			// neighbour halo, unlike meshing) — the occlusion graph gets a real mask the
			// moment a chunk is populated, so streaming regions stop leaking sightlines
			// while they wait to mesh. GreedyMeshJob refreshes it on re-mesh after edits.
			ECB.AddComponent(index, entity, new ChunkOcclusion
			                                {
				                                Mask = hasBlocks
					                                ? ChunkVisibility.ComputeMask(in blockData, in BlockPrototypes)
					                                : ChunkVisibility.AllFacesConnected
			                                });
		}
	}
}
