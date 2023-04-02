using Unity.Burst;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously = true)]
public static class NoiseGenerator
{
	[BurstCompile]
	public static float Get2DPerlin(float positionX, float positionY, float offsetX, float offsetY, float scale)
	{
		float _sampleX = positionX / scale + offsetX;
		float _sampleY = positionY / scale + offsetY;

		float2 _sampleXY = new float2(_sampleX, _sampleY);

			float _value = noise.cnoise(_sampleXY);

		return math.unlerp(-1, 1, _value);
		//return _value;
	}
}
