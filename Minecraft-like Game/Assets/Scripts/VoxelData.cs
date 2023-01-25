using Unity.Collections;
using Unity.Mathematics;

public static class VoxelData
{
	public static readonly int ChunkWidth = 16;
	public static readonly int ChunkHeight = 247;

	[ReadOnly]
	public static readonly NativeArray<int3> VoxelVertices = new NativeArray<int3>(8, Allocator.Persistent)
	{
		[0] = new int3(0, 0, 0),
		[1] = new int3(1, 0, 0),
		[2] = new int3(1, 1, 0),
		[3] = new int3(0, 1, 0),
		[4] = new int3(0, 0, 1),
		[5] = new int3(1, 0, 1),
		[6] = new int3(1, 1, 1),
		[7] = new int3(0, 1, 1)
	};

	[ReadOnly]
	public static readonly NativeArray<int> VoxelTriangles = new NativeArray<int>(24, Allocator.Persistent)
	{
		[0] = 0,	[1] = 3,	[2] = 1,	[3] = 2,
		[4] = 5,	[5] = 6,	[6] = 4,	[7] = 7,
		[8] = 3,	[9] = 7,	[10] = 2,	[11] = 6,
		[12] = 1,	[13] = 5,	[14] = 0,	[15] = 4,
		[16] = 4,	[17] = 7,	[18] = 0,	[19] = 3,
		[20] = 1,	[21] = 2,	[22] = 5,	[23] = 6
	};

	[ReadOnly] 
	public static readonly NativeArray<int2> VoxelUVs = new NativeArray<int2>(4, Allocator.Persistent)
	{
		[0] = new int2(0, 0),
		[1] = new int2(0, 1),
		[2] = new int2(1, 0),
		[3] = new int2(1, 1)
	};

	[ReadOnly]
	public static readonly NativeArray<int3> FaceChecks = new NativeArray<int3>(6, Allocator.Persistent)
	{
		[0] = new int3(0, 0, -1),
		[1] = new int3(0, 0, 1),
		[2] = new int3(0, 1, 0),
		[3] = new int3(0, -1, 0),
		[4] = new int3(-1, 0, 0),
		[5] = new int3(1, 0, 0)
	};
}