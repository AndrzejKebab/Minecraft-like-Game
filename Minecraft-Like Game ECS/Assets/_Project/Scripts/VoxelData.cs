using Unity.Burst;
using Unity.Mathematics;

namespace _Project
{
	[BurstCompile]
	public static class VoxelData
	{
		public const byte CHUNK_SIZE = 32;
		
		public static readonly int3[] FaceTangents =
			new int3[]
			{
				new(1, 0, 0),  // Z-
				new(-1, 0, 0), // Z+
				new(1, 0, 0),  // Y+
				new(-1, 0, 0), // Y-
				new(0, 0, -1), // X-
				new(0, 0, 1)   // X+
			};
		
		public static readonly int3[] FaceChecks =
			new int3[]
			{
				new(0, 0, -1), // Z-
				new(0, 0, 1),  // Z+
				new(0, 1, 0),  // Y+
				new(0, -1, 0), // Y-
				new(-1, 0, 0), // X-
				new(1, 0, 0)   // X+
			};
	}
}