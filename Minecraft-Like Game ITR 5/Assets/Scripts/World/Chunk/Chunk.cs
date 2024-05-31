using Unity.Collections;

namespace PatataStudio.World.Chunk
{
	public struct Chunk
	{
		private NativeArray<ushort> voxelMap;

		public NativeArray<ushort> VoxelMap { get => voxelMap; set => voxelMap = value; }

		public Chunk(int size, Allocator allocator)
		{
			voxelMap = new NativeArray<ushort>(size * size * size, allocator);
		}

		public void Dispose()
		{
			if (voxelMap.IsCreated)
			{
				voxelMap.Dispose();
			}
		}
	}
}