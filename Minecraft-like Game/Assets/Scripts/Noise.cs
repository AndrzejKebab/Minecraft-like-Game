using UnityEngine;

public static class Noise
{

	public static float Get2DPerlin(Vector2 position, float offset, float scale)
	{
		//float _value;
		//
		//_value = Mathf.PerlinNoise((position.x + 0.1f) / VoxelData.ChunkWidth * Scale + offset, (position.y + 0.1f) / VoxelData.ChunkWidth * Scale + offset);
		//_value = -2 * _value + 1;
		//_value *= _value;
		//
		//return Mathf.Abs(_value);

		return Mathf.PerlinNoise((position.x + 0.1f) / VoxelData.ChunkWidth * scale + offset, (position.y + 0.1f) / VoxelData.ChunkWidth * scale + offset);
	}

	public static bool Get3DPerlin(Vector3 position, float offset, float scale, float threshold)
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

		if ((AB + BC + AC + BA + CB + CA) / 6 < threshold)
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}
