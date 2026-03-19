using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public static class VoxelData
{
	public const  byte CHUNK_SIZE                   = 32;
	public const  int  WORLD_SIZE_IN_CHUNKS         = 1875000;
	public const  byte TEXTURE_ATLAS_SIZE_IN_BLOCKS = 16;
	public static byte ViewDistanceInChunks         = 8;

	public static readonly half4[] VoxelVertices =
	[
		new((half)0, (half)0, (half)0, (half)0),
		new((half)1, (half)0, (half)0, (half)0),
		new((half)1, (half)1, (half)0, (half)0),
		new((half)0, (half)1, (half)0, (half)0),
		new((half)0, (half)0, (half)1, (half)0),
		new((half)1, (half)0, (half)1, (half)0),
		new((half)1, (half)1, (half)1, (half)0),
		new((half)0, (half)1, (half)1, (half)0)
	];

	public static readonly int[] VoxelTriangles =
	[
		0, 3, 1, 2, // Z-
		5, 6, 4, 7, // Z+
		3, 7, 2, 6, // Y+
		1, 5, 0, 4, // Y-
		4, 7, 0, 3, // X-
		1, 2, 5, 6  // X+
	];

	public static readonly float2[] VoxelUVs =
	[
		new(0, 0),
		new(0, 1),
		new(1, 0),
		new(1, 1)
	];

	public static readonly int3[] FaceChecks =
	[
		new(0, 0, -1), // Z-
		new(0, 0, 1),  // Z+
		new(0, 1, 0),  // Y+
		new(0, -1, 0), // Y-
		new(-1, 0, 0), // X-
		new(1, 0, 0)   // X+
	];

	public static readonly int3[] FaceTangents =
	[
		new(1, 0, 0),  // Z-
		new(-1, 0, 0), // Z+
		new(1, 0, 0),  // Y+
		new(-1, 0, 0), // Y-
		new(0, 0, -1), // X-
		new(0, 0, 1)   // X+
	];

	public static int   WorldSizeInVoxels          => WORLD_SIZE_IN_CHUNKS * CHUNK_SIZE;
	public static float NormalizedBlockTextureSize => 1f / TEXTURE_ATLAS_SIZE_IN_BLOCKS;
}