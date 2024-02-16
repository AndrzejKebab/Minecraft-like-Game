using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public class NoiseGenerator : MonoBehaviour
{
    [BurstCompile]
    public static float Get2DPerlin(float positionX, float positionY, float offsetX, float offsetY, float scale)
    {
        var sampleX = positionX / scale + offsetX;
        var sampleY = positionY / scale + offsetY;

        var sampleXY = new float2(sampleX, sampleY);

        var value = noise.cnoise(sampleXY);

        return math.unlerp(-1, 1, value);
    }
}