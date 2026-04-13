using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
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

		[ReadOnly] public NativeArray<Block> BlockPrototypes;
		public            int                Seed,          ChunkSize, MaxCaveWorldY;
		public            float              CaveFrequency, CaveThreshold;

		public EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int index)
		{
			Entity                  entity        = Entities[index];
			int3                    chunkWorldPos = Positions[index].WorldPosition;
			NativeArray<BlockState> blockData     = ChunkDataLookup[entity].BlockData;

			var seedShift = Seed * 0.0013f;
			var offset    = new float3(31.7f, 17.3f, 53.1f);

			for (var z = 0; z < ChunkSize; z++)
			{
				var pz = (chunkWorldPos.z + z) * CaveFrequency + seedShift;
				for (var y = 0; y < ChunkSize; y++)
				{
					var worldY = chunkWorldPos.y + y;
					if (worldY > MaxCaveWorldY) continue;
					var py = worldY * CaveFrequency + seedShift;

					for (var x = 0; x < ChunkSize; x++)
					{
						var        idx   = x | (y << 5) | (z << 10);
						BlockState block = blockData[idx];

						if (block.ID == 0 ||
						    (block.ID < BlockPrototypes.Length && BlockPrototypes[block.ID].IsFluid)) continue;

						var px = (chunkWorldPos.x + x) * CaveFrequency + seedShift;
						var p  = new float3(px, py, pz);

						if (Unity.Mathematics.noise.snoise(p) * Unity.Mathematics.noise.snoise(p + offset) > CaveThreshold)
							blockData[idx] = new BlockState { ID = 0, Orientation = 0 };
					}
				}
			}

			ECB.RemoveComponent<NeedsTerrainTag>(index, entity);
			ECB.AddComponent<NeedsDecorationTag>(index, entity);
		}
	}
}