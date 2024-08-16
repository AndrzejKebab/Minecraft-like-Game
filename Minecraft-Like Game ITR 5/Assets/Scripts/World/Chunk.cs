using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PatataGames
{
	[Flags]
	public enum ChunkState : byte
	{
		Invalid = 0,
		Initialized = 1,
		Populated = 2,
		Completed = 3,
		Dirty = 4,
		Saved = 5,
		MarkedForDelete = 6,
	}

	[BurstCompile]
	public struct Chunk
	{
		public ChunkState State;
		public int3 Position;
		public IntervalList VoxelMap;

		public Chunk(int3 position, int capacity, Allocator allocator)
		{
			State = ChunkState.Initialized;
			Position = position;
			VoxelMap = new IntervalList(capacity, allocator);
		}	
		
		public Chunk(int3 position, INativeList<int> blocksIDList, int capacity, Allocator allocator)
		{
			State = ChunkState.Initialized;
			Position = position;
			VoxelMap = new IntervalList(blocksIDList, capacity, allocator);
		}

		public void Dispose()
		{
			VoxelMap.Dispose();
		}

		public override string ToString() => $"Position: X: {Position.x}, Y: {Position.y}, Z: {Position.z}, Chunk State: {State}, VoxelMap: {VoxelMap}";
	}
}