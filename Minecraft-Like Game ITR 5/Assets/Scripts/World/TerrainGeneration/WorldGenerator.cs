using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PatataStudio.World.TerrainGeneration
{
	[BurstCompile]
	public struct WorldGeneratorJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<IntPtr> IntPtrs;
		[ReadOnly] public NoiseGenerator noiseGenerator;
		[ReadOnly] public NativeList<int3> ChunksToGenerate;
		[WriteOnly] public NativeParallelHashMap<int3, Chunk>.ParallelWriter Chunks;

		public void Execute(int index)
		{
			var position = ChunksToGenerate[index];
			var chunk = GenerateChunkData(position);

			Chunks.TryAdd(position, chunk);
		}

		private Chunk GenerateChunkData(int3 position)
		{
			var chunkData = new Chunk();

			var noiseData = noiseGenerator.GenerateWorldMap(position);

			return chunkData;
		}
	}
}