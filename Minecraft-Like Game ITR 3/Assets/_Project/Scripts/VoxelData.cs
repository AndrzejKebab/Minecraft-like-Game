using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public static class VoxelData
{
	public const  byte CHUNK_SIZE           = 32;
	public static byte ViewDistanceInChunks = 8;

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

	// Each row = 4 vertex indices for one face (Z-, Z+, Y+, Y-, X-, X+)
	public static readonly int[] VoxelTriangles =
	[
		0, 3, 1, 2, // Z-
		5, 6, 4, 7, // Z+
		3, 7, 2, 6, // Y+
		1, 5, 0, 4, // Y-
		4, 7, 0, 3, // X-
		1, 2, 5, 6  // X+
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
}