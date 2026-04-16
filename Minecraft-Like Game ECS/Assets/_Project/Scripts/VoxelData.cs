using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	[BurstCompile]
	public static class ChunkData
	{
		public const  byte CHUNK_SIZE = 32;
		public const float INVERSE_CHUNK_SIZE = 1 / (float)CHUNK_SIZE;
		public static int3 ChunkSize  = new((int)CHUNK_SIZE);

		[RuntimeInitializeOnLoadMethod]
		private static void Init()
		{
			ChunkSize = new int3((int)CHUNK_SIZE);
		}
	}
}