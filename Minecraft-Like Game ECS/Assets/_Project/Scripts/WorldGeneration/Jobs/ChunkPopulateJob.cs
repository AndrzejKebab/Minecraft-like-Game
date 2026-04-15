using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
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
	/// Single fused population job.  Per chunk:
	///   1. Halo heightmap (3×3 chunk area)
	///   2. Terrain blocks (own 32³)
	///   3. Caves (own 32³)
	///   4. Ores (own chunk only)
	///   5. Trees (project from 27 halo columns into own chunk)
	///   6. Tag IsPopulated + NeedsMeshSync (or IsEmpty)
	///
	/// Fully deterministic per chunk.  No neighbor BlockData reads or writes.
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		FloatPrecision = FloatPrecision.Low)]
	public struct ChunkPopulateJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<Entity>                 Entities;
		[ReadOnly] public NativeArray<ChunkPositionComponent> Positions;

		[NativeDisableContainerSafetyRestriction]
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;

		[ReadOnly] public NativeArray<Block>       BlockPrototypes;
		[ReadOnly] public NativeArray<OreSettings> OreTypes;

		public int Seed;
		public int ChunkSize;

		public ushort AirID;
		public ushort GrassID;
		public ushort LogID;
		public ushort LeavesID;

		public float TreeDensity;
		public int   MinTrunkHeight;
		public int   MaxTrunkHeight;

		public FastNoise   ContinentalnessNoise;
		public FastNoise   PeaksAndValleysNoise;
		public FastNoise   ErosionNoise;
		public FastNoise   RiverNoise;
		public FastNoise   CavesNoise;
		public NativeCurve ContinentalnessCurve;
		public NativeCurve ErosionCurve;
		public NativeCurve PeaksAndValleysCurve;

		public EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			// ── 1. Halo heightmap ──────────────────────────────────────────────
			NoiseGeneratorHalo.GenerateHaloHeightmap(
				out NativeArray<int> haloHeights,
				ref ContinentalnessNoise, ref PeaksAndValleysNoise,
				ref ErosionNoise, ref RiverNoise,
				ref chunkWorldPos, ChunkSize, Seed,
				ref ContinentalnessCurve, ref ErosionCurve, ref PeaksAndValleysCurve);

			// ── 2. Terrain ─────────────────────────────────────────────────────
			//      Center 32×32 of halo = own chunk's heights.  No extra noise calls.
			int haloSize = ChunkSize * 3;
			int centerOffset = ChunkSize; // halo origin is -ChunkSize from own origin

			for (int z = 0; z < ChunkSize; z++)
			for (int x = 0; x < ChunkSize; x++)
			{
				int hx     = x + centerOffset;
				int hz     = z + centerOffset;
				int height = haloHeights[hx + hz * haloSize];

				for (int y = 0; y < ChunkSize; y++)
				{
					int    worldY = chunkWorldPos.y + y;
					ushort id     = NoiseGenerator.ClassifyVoxel(worldY, height);
					int    idx    = x | (y << 5) | (z << 10);
					blockData[idx] = new BlockState { ID = id, Orientation = 0 };
				}
			}

			// ── 3. Caves ───────────────────────────────────────────────────────
			NoiseGenerator.GenerateCaveMap(out NativeTexture3D<float> caveMap,
			                               ref CavesNoise, ref chunkWorldPos, ChunkSize, Seed);

			for (int z = 0; z < ChunkSize; z++)
			for (int y = 0; y < ChunkSize; y++)
			for (int x = 0; x < ChunkSize; x++)
			{
				int        idx   = x | (y << 5) | (z << 10);
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
			TreeGeneratorDeterministic.ProjectHaloTreesIntoChunk(
				ref blockData, ref haloHeights,
				ref CavesNoise,
				ref chunkWorldPos, ChunkSize, Seed,
				TreeDensity, MinTrunkHeight, MaxTrunkHeight,
				AirID, GrassID, LogID, LeavesID);

			haloHeights.Dispose();

			// ── 6. Emptiness check + tags ──────────────────────────────────────
			bool hasBlocks = false;
			for (int i = 0; i < blockData.Length; i++)
			{
				if (blockData[i].ID != 0) { hasBlocks = true; break; }
			}

			ECB.RemoveComponent<NeedsPopulation>(index, entity);
			ECB.AddComponent<IsPopulated>(index, entity);

			if (hasBlocks) ECB.AddComponent<NeedsMeshSync>(index, entity);
			else           ECB.AddComponent<IsEmpty>(index, entity);
		}
	}
}