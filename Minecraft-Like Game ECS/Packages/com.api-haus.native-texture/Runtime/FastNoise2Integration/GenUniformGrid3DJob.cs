namespace NativeTexture.FastNoise2.Jobs
{
  using global::FastNoise2.Bindings;
  using Unity.Burst;
  using Unity.Collections;
  using Unity.Jobs;
  using Unity.Mathematics;
  using static global::FastNoise2.Bindings.FastNoise;

  [BurstCompile]
  public struct GenUniformGrid3DJob : IJob
  {
    public NativeTexture3D<float> texture;
    public FastNoise noise;
    public int seed;
    public float3 offset;
    public float3 stepSize;
    public NativeReference<OutputMinMax> outputMinMax;

    public unsafe void Execute()
    {
      OutputMinMax local = default;
      noise.GenUniformGrid3D(
        texture.GetUnsafePtr(),
        &local,
        offset.x,
        offset.y,
        offset.z,
        texture.Width,
        texture.Height,
        texture.Depth,
        stepSize.x,
        stepSize.y,
        stepSize.z,
        seed
      );

      if (outputMinMax.IsCreated)
        outputMinMax.Value = local;
    }
  }
}
