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
		///     The inner arrays are owned by TerraTileCacheSingleton; this job is a
		///     registered reader via the tile's ReadHandle.
		/// </summary>
		[NativeDisableContainerSafetyRestriction]
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

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			// ── 1. Tile columns (generated + eroded once per tile) ─────────────
			TerraTileSlice slice   = TileSlices[index];
			var            gen     = TerraTileConst.GEN_BLOCKS;
			var            baseX   = chunkWorldPos.x - slice.OriginX;
			var            baseZ   = chunkWorldPos.z - slice.OriginZ;

			// vertical bounds of this chunk's own 32×32 columns
			var minSurface = int.MaxValue;
			var maxTop     = int.MinValue;
			for (var z = 0; z < ChunkSize; z++)
			for (var x = 0; x < ChunkSize; x++)
			{
				TerraColumn column = slice.Columns[baseX + x + (baseZ + z) * gen];
				minSurface = math.min(minSurface, column.SurfaceY);
				maxTop     = math.max(maxTop, math.max(column.SurfaceY, column.WaterY));
			}

			var worldYMin = chunkWorldPos.y;
			var worldYMax = chunkWorldPos.y + ChunkSize - 1;

			// ── fast path: fully above terrain and water → empty chunk ─────────
			if (worldYMin > maxTop)
			{
				var air = new BlockState { ID = 0, Orientation = 0 };
				for (var i = 0; i < blockData.Length; i++) blockData[i] = air;

				ECB.RemoveComponent<NeedsPopulation>(index, entity);
				ECB.AddComponent<IsPopulated>(index, entity);
				ECB.AddComponent<IsEmpty>(index, entity);
				return;
			}

			// ── 2. Terrain ─────────────────────────────────────────────────────
			if (worldYMax < minSurface - 4)
			{
				// fast path: fully below every surface layer → solid stone
				var stone = new BlockState { ID = TerraGenerator.STONE, Orientation = 0 };
				for (var i = 0; i < blockData.Length; i++) blockData[i] = stone;
			}
			else
			{
				// mountain surfaces above this world Y turn to bare stone
				var stoneLineY = (int)(TerraSettings.WorldHeight * 0.62f) - TerraSettings.SeaLevel;

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

			// ── 3. Caves ───────────────────────────────────────────────────────
			NoiseGenerator.GenerateCaveMap(out NativeTexture3D<float> caveMap,
			                               ref CavesNoise, ref chunkWorldPos, ChunkSize, Seed);

			for (var z = 0; z < ChunkSize; z++)
			for (var y = 0; y < ChunkSize; y++)
			for (var x = 0; x < ChunkSize; x++)
			{
				var        idx   = x | (y << 5) | (z << 10);
				BlockState block = blockData[idx];
				if (block.ID == 0) continue;
				if (block.ID < BlockPrototypes.Length && BlockPrototypes[block.ID].IsFluid) continue;

				if (caveMap[new int3(x, y, z)] > 0)
					blockData[idx] = new BlockState { ID = 0, Orientation = 0 };
			}

			caveMap.Dispose();

			// ── 4. Ores ────────────────────────────────────────────────────────
			OreGeneratorLocal.Generate(ref blockData, ref OreTypes, ref chunkWorldPos, ChunkSize, Seed);

			// ── 5. Trees (deterministic tile projection) ───────────────────────
			//      TODO reenable using slice.Columns — the tile border guarantees the
			//      full 1-chunk halo (SurfaceY + Biome) is available for foreign trees
			/*TreeGeneratorDeterministic.ProjectHaloTreesIntoChunk(...);*/

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
		}
	}
}
