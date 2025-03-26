using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime;

namespace PatataGames;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct PopulateVoxelMapParallel : IJobFor
{
	[NativeDisableContainerSafetyRestriction]
	[WriteOnly] public   NativeParallelHashMap<int3, Chunk>.ParallelWriter ChunkMap;
	[ReadOnly] public NativeList<int3>                                  ChunksToUpdate;
	
	public void Execute(int index)
	{
		int3 chunkPos = ChunksToUpdate[index];
		ChunkMap.TryAdd(chunkPos, PopulateChunk(chunkPos));
	}

	private Chunk PopulateChunk(int3 chunkPos)
	{
		Chunk chunk = new Chunk();
		chunk.VoxelMap = new NativeArray<short>(ChunkSystem.ChunkSize * ChunkSystem.ChunkSize * ChunkSystem.ChunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		
		for (int x = 0; x < ChunkSystem.ChunkSize; x++)
		{
			for (int y = 0; y < ChunkSystem.ChunkSize; y++)
			{
				for (int z = 0; z < ChunkSystem.ChunkSize; z++)
				{
					var pos        = new int3(chunkPos.x + x, chunkPos.y + y, chunkPos.z + z);
					var  noiseValue = noise.snoise(pos) * 512f + 128;
					chunk.VoxelMap.SetAtFlatIndex(ChunkSystem.ChunkSize, x, y, z,
					                                     noiseValue > 256 ? (short)0 : (short)1);
				}
			}
		}
		
		return chunk;
	}
}