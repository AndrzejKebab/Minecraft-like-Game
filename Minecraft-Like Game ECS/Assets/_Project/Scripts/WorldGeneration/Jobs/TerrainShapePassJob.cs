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

		public            FastNoise          ContinentalnessNoise;
		public            FastNoise          PeaksAndValleysNoise;
		public            FastNoise          ErosionNoise;
		[ReadOnly] public NativeArray<Block> BlockPrototypes;
		[ReadOnly] public NativeCurve        BiomeHeight, ErosionCurve, PeaksAndValleysCurve;
		public            int                Seed,        ChunkSize;

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			NoiseGenerator.GenerateTerrainMap(out NativeTexture2D<int> heightMap, ref ContinentalnessNoise, ref PeaksAndValleysNoise, ref ErosionNoise, ref chunkWorldPos, ChunkSize, Seed,
				ref BiomeHeight, ref PeaksAndValleysCurve, ref ErosionCurve);

			for (var x = 0; x < ChunkSize; x++)
			for (var z = 0; z < ChunkSize; z++)
			{
				for (var y = 0; y < ChunkSize; y++)
				{
					var id = NoiseGenerator.ClassifyVoxel(chunkWorldPos.y + y, heightMap[x,z]);
					id = id < BlockPrototypes.Length ? BlockPrototypes[id].ID : (ushort)0;
					blockData[x | (y << 5) | (z << 10)] = new BlockState { ID = id, Orientation = 0 };
				}
			}

			heightMap.Dispose();
		}
	}
}