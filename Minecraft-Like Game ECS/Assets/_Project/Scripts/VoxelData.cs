using Unity.Burst;
using Unity.Mathematics;

namespace _Project
{
	[BurstCompile]
	public static class ChunkData
	{
		public const  byte CHUNK_SIZE = 32;
		public static int3 ChunkSize  = new((int)CHUNK_SIZE);
	}
}