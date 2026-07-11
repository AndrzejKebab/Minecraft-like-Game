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
	///     1. TerraGen halo columns (3×3 chunk area) — ReTerraForged-style pipeline:
	///        continent → terrain regions → populator blend → rivers → climate/biomes
	///     2. Terrain blocks (own 32³), biome-aware surfaces + river/sea water
	///     3. Caves (own 32³, FastNoise2)
	///     4. Ores (own chunk only)
	///     5. Trees (project from halo columns into own chunk)
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

			// ── 1. TerraGen halo columns ───────────────────────────────────────
			TerraGenerator.GenerateHaloColumns(out NativeArray<TerraColumn> haloColumns,
			                                   ref chunkWorldPos, ChunkSize, in TerraSettings);

			// ── 2. Terrain ─────────────────────────────────────────────────────
			//      Center 32×32 of halo = own chunk's columns.  No extra noise calls.
			var haloSize     = ChunkSize * 3;
			var centerOffset = ChunkSize; // halo origin is -ChunkSize from own origin

			// mountain surfaces above this world Y turn to bare stone
			var stoneLineY = (int)(TerraSettings.WorldHeight * 0.62f) - TerraSettings.SeaLevel;

			for (var z = 0; z < ChunkSize; z++)
			for (var x = 0; x < ChunkSize; x++)
			{
				var hx     = x + centerOffset;
				var hz     = z + centerOffset;
				TerraColumn column = haloColumns[hx + hz * haloSize];

				for (var y = 0; y < ChunkSize; y++)
				{
					var worldY = chunkWorldPos.y + y;
					var id     = TerraGenerator.ClassifyVoxel(worldY, in column, stoneLineY);
					var idx    = x | (y << 5) | (z << 10);
					blockData[idx] = new BlockState { ID = id, Orientation = 0 };
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

			// ── 5. Trees (deterministic halo projection) ───────────────────────
			//      TODO reenable using haloColumns (SurfaceY + Biome give ground level
			//      and biome-specific density for foreign trees rooted in neighbors)
			/*TreeGeneratorDeterministic.ProjectHaloTreesIntoChunk(...);*/

			haloColumns.Dispose();

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
