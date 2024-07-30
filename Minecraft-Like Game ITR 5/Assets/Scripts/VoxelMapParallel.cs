using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PatataStudio
{
	public struct VoxelMapParallel : IJobParallelFor
	{
		public NativeList<int3> ChunksToUpdate;
		public NativeParallelHashMap<int3, Chunk> ChunkMap;
		public NativeParallelHashMap<int3, Chunk>.ReadOnly ChunkAccessor;

		public void Execute(int index)
		{
			throw new System.NotImplementedException();
		}
	}
}