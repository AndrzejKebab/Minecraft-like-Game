using Unity.Burst;

namespace _Project.WorldGeneration.Blocks
{
	[BurstCompile]
	public struct BlockState
	{
		public ushort ID;
		public byte   Orientation;

		public bool IsEmpty => ID == 0;
	}
}