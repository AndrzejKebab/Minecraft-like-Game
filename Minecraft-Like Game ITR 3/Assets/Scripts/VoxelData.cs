using Unity.Mathematics;

public static class VoxelData
{
    public const byte ChunkSize = 32;
    public const int WorldSizeInChunks = 1875000;
    public static int WorldSizeInVoxels => WorldSizeInChunks * ChunkSize;
    public static byte ViewDistanceInChunks = 8;
    public const byte TextureAtlasSizeInBlocks = 16;
    public static float NormalizedBlockTextureSize => 1f / TextureAtlasSizeInBlocks;

    public static readonly half4[] VoxelVertices =
    {
        new((half)0, (half)0, (half)0, (half)0),
        new((half)1, (half)0, (half)0, (half)0),
        new((half)1, (half)1, (half)0, (half)0),
        new((half)0, (half)1, (half)0, (half)0),
        new((half)0, (half)0, (half)1, (half)0),
        new((half)1, (half)0, (half)1, (half)0),
        new((half)1, (half)1, (half)1, (half)0),
        new((half)0, (half)1, (half)1, (half)0)
    };

    public static readonly int[] VoxelTriangles =
    {
        0, 3, 1, 2,
        5, 6, 4, 7,
        3, 7, 2, 6,
        1, 5, 0, 4,
        4, 7, 0, 3,
        1, 2, 5, 6
    };

    public static readonly float2[] VoxelUVs =
    {
        new(0, 0),
        new(0, 1),
        new(1, 0),
        new(1, 1)
    };

    public static readonly int3[] FaceChecks =
    {
        new(0, 0, -1),
        new(0, 0, 1),
        new(0, 1, 0),
        new(0, -1, 0),
        new(-1, 0, 0),
        new(1, 0, 0)
    };
}