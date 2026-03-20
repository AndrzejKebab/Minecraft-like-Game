using FastNoise2.Bindings;
using NativeTexture;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
///     Central noise generation utility. All chunk terrain sampling goes through here so
///     biome layering, curve evaluations, and domain warps can be added in one place.
///     Noise convention: raw FastNoise2 output in [-1, 1].
///     0  = water level / solid ground surface
///     +1  = maximum terrain height above ground
///     -1  = maximum depth below ground
/// </summary>
public static class NoiseGenerator
{
	// -------------------------------------------------------------------------
	// Heightmap generation (main-thread, synchronous, SIMD-accelerated)
	// -------------------------------------------------------------------------

	/// <summary>
	///     Generates a CHUNK_SIZE × CHUNK_SIZE heightmap for the given chunk.
	///     Returns raw [-1, 1] values — no normalization applied.
	///     Caller is responsible for disposing the returned texture.
	/// </summary>
	public static NativeTexture2D<float> GenerateHeightMap(
		ref FastNoise noise,
		int3          chunkWorldPos,
		int           chunkSize,
		int           biomeScale,
		int           seed)
	{
		var heightMap = new NativeTexture2D<float>(
		                                           new int2(chunkSize, chunkSize),
		                                           Allocator.Persistent);
		var stepSize = 1f / biomeScale;
		unsafe
		{
			noise.GenUniformGrid2D(heightMap.GetUnsafePtr(), null, chunkWorldPos.x, chunkWorldPos.z, chunkSize,
			                       chunkSize, stepSize, stepSize, seed); // discard min/max — raw values used directly );
		}


		return heightMap;
	}
	
	// -------------------------------------------------------------------------
	// Voxel classification (shared between PopulateVoxelMapJob and ChunkJob border fallback)
	// -------------------------------------------------------------------------

	/// <summary>
	///     Converts a raw [-1, 1] noise height sample into a terrain height in voxels.
	/// </summary>
	public static int HeightFromNoise(float rawNoise, int biomeHeight, int solidGroundHeight)
	{
		// rawNoise 0 → solidGroundHeight (water/ground level)
		// rawNoise 1 → solidGroundHeight + biomeHeight (mountain peak)
		// rawNoise -1 → solidGroundHeight - biomeHeight (deepest valley)
		return (int)(rawNoise * biomeHeight) + solidGroundHeight;
	}

	/// <summary>
	///     Classifies a voxel position given the computed terrain height.
	///     Block IDs: 0=air, 1=bedrock, 2=stone, 3=dirt, 4=grass, 5=water.
	/// </summary>
	public static ushort ClassifyVoxel(int yPos, int terrainHeight, int solidGroundHeight)
	{
		if (yPos > terrainHeight) return yPos <= solidGroundHeight ? (ushort)5 : (ushort)0; // water or air
		if (yPos == terrainHeight) return 4;                                                // grass
		return yPos > terrainHeight - 6 ? (ushort)3 : (ushort)                              // dirt
			       2;                                                                       // stone
	}
}