using Unity.Collections;
using Unity.Mathematics;

namespace PatataGames;

public struct Chunk()
{
	public NativeArray<short> VoxelMap = new((int)math.pow(ChunkSystem.CHUNK_SIZE, 3), Allocator.Persistent);
}