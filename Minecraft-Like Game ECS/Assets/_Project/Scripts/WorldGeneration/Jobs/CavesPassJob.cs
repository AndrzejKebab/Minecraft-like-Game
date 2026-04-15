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
	[BurstCompile]
	public struct CavesPassJob : IJobFor
	{
		[ReadOnly] public NativeArray<Entity>                 Entities;
		[ReadOnly] public NativeArray<ChunkPositionComponent> Positions;

		[NativeDisableContainerSafetyRestriction]
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;

		[ReadOnly] public NativeArray<Block>                 BlockPrototypes;
		public            int                                Seed, ChunkSize;
		public            FastNoise                          CaveNoise;
		public            EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;
			
			NoiseGenerator.GenerateCaveMap(out NativeTexture3D<float> caveMap, ref CaveNoise, ref chunkWorldPos, ChunkSize, Seed);
			
			for (var z = 0; z < ChunkSize; z++)
			{
				for (var y = 0; y < ChunkSize; y++)
				{

					for (var x = 0; x < ChunkSize; x++)
					{
						var        idx   = x | (y << 5) | (z << 10);
						BlockState block = blockData[idx];

						if (block.ID == 0 ||
						    (block.ID < BlockPrototypes.Length && BlockPrototypes[block.ID].IsFluid)) continue;


						if (caveMap[new int3(x, y, z)] > 0)
							blockData[idx] = new BlockState { ID = 0, Orientation = 0 };
					}
				}
			}

			ECB.RemoveComponent<NeedsTerrainTag>(index, entity);
			ECB.AddComponent<NeedsDecorationTag>(index, entity);
		}
	}
}