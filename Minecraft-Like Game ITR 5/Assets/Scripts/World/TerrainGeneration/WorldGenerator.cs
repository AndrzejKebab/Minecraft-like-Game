using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PatataStudio.World.TerrainGeneration
{
	[BurstCompile]
	public struct WorldGeneratorJob : IJobParallelFor
	{
		[ReadOnly] public NoiseGenerator noiseGenerator;
		[ReadOnly] public NativeList<int3> ChunksToGenerate;
		[WriteOnly] public NativeParallelHashMap<int3, ChunkComponent>.ParallelWriter Chunks;

		public void Execute(int index)
		{
			var position = ChunksToGenerate[index];
			var chunk = GenerateChunkData(position);

			Chunks.TryAdd(position, chunk);
		}

		private ChunkComponent GenerateChunkData(int3 position)
		{
			var chunkData = new ChunkComponent();

			var temp = new NoiseData();
			var noiseData = noiseGenerator.GenerateWorldMap(NoiseType.Continentalness, temp);

			return default;
		}
	}
}