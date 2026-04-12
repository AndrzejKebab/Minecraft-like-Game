namespace NativeTexture.FastNoise2.Jobs
{
  using global::FastNoise2.Bindings;
  using Unity.Burst;
  using Unity.Collections;
  using Unity.Jobs;
  using Unity.Mathematics;
  using static global::FastNoise2.Bindings.FastNoise;

  [BurstCompile]
  public struct GenTileable2DJob : IJob
  {
    public NativeTexture2D<float> texture;
    public FastNoise noise;
    public int seed;
    public float2 stepSize;
    public NativeReference<OutputMinMax> outputMinMax;

    public unsafe void Execute()
    {
      OutputMinMax local = default;
      noise.GenTileable2D(
        texture.GetUnsafePtr(),
        &local,
        texture.Width,
        texture.Height,
        stepSize.x,
        stepSize.y,
        seed
      );

      if (outputMinMax.IsCreated)
        outputMinMax.Value = local;
    }
  }
}
