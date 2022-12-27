using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HeightMap
{
	public static float Get2DPerlin(Vector2 position, float offset, float scale)
	{
		return Mathf.PerlinNoise((position.x + 0.1f) / VoxelData.ChunkWidth * scale + offset, (position.y + 0.1f) / VoxelData.ChunkWidth * scale + offset);
	}

	public static bool Get3DPerlin(Vector3 position, float offset, float scale, float minThreshold, float maxThreshold)
	{
		float x = (position.x + offset + 0.1f) * scale;
		float y = (position.y + offset + 0.1f) * scale;
		float z = (position.z + offset + 0.1f) * scale;

		float AB = Mathf.PerlinNoise(x, y);
		float BC = Mathf.PerlinNoise(y, z);
		float AC = Mathf.PerlinNoise(x, z);

		float BA = Mathf.PerlinNoise(y, x);
		float CB = Mathf.PerlinNoise(z, y);
		float CA = Mathf.PerlinNoise(z, x);

		if((AB + BC + AC + BA + CB + CA) / 6 >= minThreshold && (AB + BC + AC + BA + CB + CA) / 6 <= maxThreshold)
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}
