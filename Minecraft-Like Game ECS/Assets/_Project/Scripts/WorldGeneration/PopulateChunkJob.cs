using _Project.WorldGeneration.Blocks;
using FastNoise2.Bindings;
using NativeTexture;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct PopulateChunkJob : IJob
	{
		public            NativeArray<ushort> BlockData;
		[ReadOnly] public NativeArray<Block>  BlockPrototypes;

		public NativeReference<bool> IsDirty;
		
		public FastNoise Noise;
		public int3      ChunkWorldPos;
		public int       ChunkSize;
		public int BiomeHeight;
		public int       Seed;

		public void Execute()
		{
			NoiseGenerator.GenerateHeightMap(
			                                 out NativeTexture2D<float> heightMap,
			                                 ref Noise,
			                                 ref ChunkWorldPos,
			                                 ChunkSize,
			                                 Seed);

			for (var x = 0; x < ChunkSize; x++)
			for (var z = 0; z < ChunkSize; z++)
			{
				var rawNoise      = heightMap[new int2(x, z)];
				var terrainHeight = NoiseGenerator.HeightFromNoise(rawNoise, BiomeHeight);

				for (var y = 0; y < ChunkSize; y++)
				{
					var worldY = ChunkWorldPos.y + y;
					var id     = NoiseGenerator.ClassifyVoxel(worldY, terrainHeight);
					id = id < BlockPrototypes.Length ? BlockPrototypes[id].ID : (ushort)0;
					if (id != 0)
					{
						IsDirty.Value = true;
					}
					BlockData.SetAtIndex(x,y,z, id);
				}
			}

			heightMap.Dispose();
		}
	}
}