using Unity.Collections;
using Unity.Mathematics;

public static class VoxelData
{
	public static readonly byte ChunkSize = 32;

	public static readonly int WorldSizeInChunks = 1875000; // 100000 block + 32x32 = 7274

	public static int WorldSizeInVoxels
	{
		get
		{
			return WorldSizeInChunks * ChunkSize;
		}
	}

	public static byte ViewDistanceInChunks = 8;

	public static readonly byte TextureAtlasSizeInBlocks = 16;

	public static float NormalizedBlockTextureSize
	{
		get
		{
			return 1f / (float)TextureAtlasSizeInBlocks;
		}
	}

	public static readonly NativeArray<half4> VoxelVertices = new NativeArray<half4>(8, Allocator.Persistent)
	{
		[0] = new half4((half)0, (half)0, (half)0, (half)0),
		[1] = new half4((half)1, (half)0, (half)0, (half)0),
		[2] = new half4((half)1, (half)1, (half)0, (half)0),
		[3] = new half4((half)0, (half)1, (half)0, (half)0),
		[4] = new half4((half)0, (half)0, (half)1, (half)0),
		[5] = new half4((half)1, (half)0, (half)1, (half)0),
		[6] = new half4((half)1, (half)1, (half)1, (half)0),
		[7] = new half4((half)0, (half)1, (half)1, (half)0)
	};

	public static readonly NativeArray<int> VoxelTriangles = new NativeArray<int>(24, Allocator.Persistent)
	{
		[0] = 0,  [1] = 3,  [2] = 1,  [3] = 2,
		[4] = 5,  [5] = 6,  [6] = 4,  [7] = 7,
		[8] = 3,  [9] = 7,  [10] = 2, [11] = 6,
		[12] = 1, [13] = 5, [14] = 0, [15] = 4,
		[16] = 4, [17] = 7, [18] = 0, [19] = 3,
		[20] = 1, [21] = 2, [22] = 5, [23] = 6
	};

	public static readonly NativeArray<float2> VoxelUVs = new NativeArray<float2>(4, Allocator.Persistent)
	{
		[0] = new float2(0, 0),
		[1] = new float2(0, 1),
		[2] = new float2(1, 0),
		[3] = new float2(1, 1)
	};

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