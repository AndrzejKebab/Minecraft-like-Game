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
	[BurstCompile]
	public struct TerrainShapePassJob : IJobFor
	{
		[ReadOnly] public NativeArray<Entity>                 Entities;
		[ReadOnly] public NativeArray<ChunkPositionComponent> Positions;

		[NativeDisableContainerSafetyRestriction]
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;

		public            FastNoise          Noise;
		[ReadOnly] public NativeArray<Block> BlockPrototypes;
		[ReadOnly] public NativeCurve        BiomeHeight, ErosionCurve, PeaksAndValleysCurve;
		public            int                Seed,        ChunkSize;

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			NoiseGenerator.GenerateHeightMap(out NativeTexture2D<float> heightMap, ref Noise, ref chunkWorldPos,
			                                 ChunkSize, Seed);

			for (var x = 0; x < ChunkSize; x++)
			for (var z = 0; z < ChunkSize; z++)
			{
				var terrainHeight = NoiseGenerator.HeightFromNoise(heightMap[new int2(x, z)], in BiomeHeight,
				                                                   in ErosionCurve, in PeaksAndValleysCurve);
				for (var y = 0; y < ChunkSize; y++)
				{
					var id = NoiseGenerator.ClassifyVoxel(chunkWorldPos.y + y, terrainHeight);
					id                                  = id < BlockPrototypes.Length ? BlockPrototypes[id].ID : (ushort)0;
					blockData[x | (y << 5) | (z << 10)] = new BlockState { ID = id, Orientation = 0 };
				}
			}

			heightMap.Dispose();
		}
	}
}