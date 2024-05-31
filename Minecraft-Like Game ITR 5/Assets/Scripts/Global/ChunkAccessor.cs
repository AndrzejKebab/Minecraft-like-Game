using PatataStudio.World.Chunk;
using Unity.Collections;
using Unity.Mathematics;

namespace PatataStudio.Global
{
	public struct ChunkAccessor
	{
		public NativeParallelHashMap<int3, Chunk>.ReadOnly ChunkMap;
	}
}