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
	/// Terrain shape pass: fills ChunkComponent.BlockData with surface terrain + water.
	/// Intentionally does NOT carve caves — that is CavesPassJob's job.
	///
	/// Per Execute call:
	///   1. GenerateTerrainMap  →  32×32 int heightmap
	///   2. Per-voxel loop      →  ClassifyVoxel → BlockState
	///   3. Dispose heightmap
	///
	/// NativeDisableContainerSafetyRestriction on ChunkDataLookup is safe because each
	/// Entity maps to a distinct BlockData array. Long-term: migrate to DynamicBuffer
	/// + BufferLookup which the safety system can verify.
	///
	/// IMPORTANT — ChunkPopulateSystem must pass settings.RiverNoise to this job's
	/// RiverNoise field. See WorldComponents.cs for the new field declaration.
	/// </summary>
	[BurstCompile]
	public struct TerrainShapePassJob : IJobFor
	{
		[ReadOnly] public NativeArray<Entity>                 Entities;
		[ReadOnly] public NativeArray<ChunkPositionComponent> Positions;

		[NativeDisableContainerSafetyRestriction]
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;

		// ── Noise generators ─────────────────────────────────────────────────
		// FastNoise2 encoded node trees set by ChunkPopulateSystem from WorldSettingsSingleton.
		public FastNoise ContinentalnessNoise;  // FractalFBm  Simplex2D freq~0.0006
		public FastNoise PeaksAndValleysNoise;  // FractalRidged Simplex2D freq~0.004
		public FastNoise ErosionNoise;           // FractalFBm  Simplex2D freq~0.0015
		public FastNoise RiverNoise;             // FractalFBm  Simplex2D freq~0.001 (abs() in code)

		// ── Spline curves ─────────────────────────────────────────────────────
		// Field names kept matching ChunkPopulateSystem assignment.
		//   BiomeHeight        ← settings.ContinentalnessCurve  cont[-1,1] → base height (blocks)
		//   ErosionCurve       ← settings.ErosionCurve           eros[-1,1] → factor [0,1]
		//   PeaksAndValleysCurve ← settings.PeaksAndValleysCurve pv  [0,1]  → bonus height (blocks)
		[ReadOnly] public NativeCurve BiomeHeight;
		[ReadOnly] public NativeCurve ErosionCurve;
		[ReadOnly] public NativeCurve PeaksAndValleysCurve;

		[ReadOnly] public NativeArray<Block> BlockPrototypes;
		public            int                Seed, ChunkSize;

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			// Pass 1: generate heightmap (all 4 noise maps sampled, rivers carved)
			NoiseGenerator.GenerateTerrainMap(
				out NativeTexture2D<int> heightMap,
				ref ContinentalnessNoise,
				ref PeaksAndValleysNoise,
				ref ErosionNoise,
				ref RiverNoise,
				ref chunkWorldPos,
				ChunkSize, Seed,
				ref BiomeHeight,
				ref ErosionCurve,
				ref PeaksAndValleysCurve);

			// Pass 2: classify every voxel in this chunk
			for (var x = 0; x < ChunkSize; x++)
			for (var z = 0; z < ChunkSize; z++)
			{
				int terrainHeight = heightMap[x, z];

				for (var y = 0; y < ChunkSize; y++)
				{
					int    worldY     = chunkWorldPos.y + y;
					ushort protoIndex = NoiseGenerator.ClassifyVoxel(worldY, terrainHeight);

					// Guard: protoIndex must be valid in BlockPrototypes array
					ushort id = protoIndex < BlockPrototypes.Length
						? BlockPrototypes[protoIndex].ID
						: (ushort)0;

					// 1D flat index: x in [0..4], y in [5..9], z in [10..14]
					// Valid only for CHUNK_SIZE = 32 (2^5). Update shifts if size changes.
					blockData[x | (y << 5) | (z << 10)] = new BlockState { ID = id, Orientation = 0 };
				}
			}

			heightMap.Dispose();
		}
	}
}