using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class NoiseGenerator
{
	public static float Get2DPerlin(float2 position, float2 offset, float scale)
	{
			float _sampleX = position.x / scale + offset.x;
			float _sampleY = position.y / scale + offset.y;

			float _value = noise.cnoise(new float2(_sampleX, _sampleY));

			return Mathf.InverseLerp(-1, 1, _value);
			//return _value;
	}
}
