using PatataStudio.Global;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PatataStudio.World.Chunk
{
	public struct ChunkParallelJob : IJobParallelFor
	{
		public ChunkAccessor Accessor;
		public NativeList<int3> ChunksToUpdate;
		public NativeParallelHashMap<int3, Chunk> ChunkMap;

		public void Execute(int index)
		{
			throw new System.NotImplementedException();
		}
	}
}