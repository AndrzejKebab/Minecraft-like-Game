using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PatataStudio
{
	[BurstCompile]
	public struct Chunk
	{
		public int3 Position;
		public IntervalList VoxelMap;

		public Chunk(int3 position, int capacity, Allocator allocator)
		{
			Position = position;
			VoxelMap = new IntervalList(capacity, allocator);
		}	
		
		public Chunk(int3 position, INativeList<int> blocksIDList, int capacity, Allocator allocator)
		{
			Position = position;
			VoxelMap = new IntervalList(blocksIDList, capacity, allocator);
		}

		public void Dispose()
		{
			VoxelMap.Dispose();
		}

		public override string ToString() => $"Position: X: {Position.x}, Y: {Position.y}, Z: {Position.z}, VoxelMap: {VoxelMap}";
	}
}