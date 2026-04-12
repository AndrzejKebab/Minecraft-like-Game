using Unity.Burst;
using Unity.Mathematics;

namespace _Project
{
	[BurstCompile]
	public static class VoxelData
	{
		public const byte CHUNK_SIZE = 32;
	}
}