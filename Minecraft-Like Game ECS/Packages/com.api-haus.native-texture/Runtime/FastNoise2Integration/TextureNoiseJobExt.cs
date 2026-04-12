namespace NativeTexture.FastNoise2
{
  using global::FastNoise2.Bindings;
  using Jobs;
  using Unity.Collections;
  using Unity.Jobs;
  using Unity.Mathematics;
  using static global::FastNoise2.Bindings.FastNoise;

  /// <summary>
  /// Extension methods for scheduling FastNoise2 generation jobs on NativeTexture containers.
  /// </summary>
  public static class TextureNoiseJobExt
  {
    public static JobHandle GenUniformGrid2D(
      this FastNoise noise,
      NativeTexture2D<float> texture,
      NativeReference<OutputMinMax> outputMinMax,
      int seed,
      float2 offset,
      float2 stepSize,
      JobHandle dependency = default
    ) =>
      new GenUniformGrid2DJob
      {
        noise = noise,
        texture = texture,
        seed = seed,
        offset = offset,
        stepSize = stepSize,
        outputMinMax = outputMinMax,
      }.Schedule(dependency);

    public static JobHandle GenTileable2D(
      this FastNoise noise,
      NativeTexture2D<float> texture,
      NativeReference<OutputMinMax> outputMinMax,
      int seed,
      float2 stepSize,
      JobHandle dependency = default
    ) =>
      new GenTileable2DJob
      {
        noise = noise,
        texture = texture,
        seed = seed,
        stepSize = stepSize,
        outputMinMax = outputMinMax,
      }.Schedule(dependency);

    public static JobHandle GenUniformGrid3D(
      this FastNoise noise,
      NativeTexture3D<float> texture,
      NativeReference<OutputMinMax> outputMinMax,
      int seed,
      float3 offset,
      float3 stepSize,
      JobHandle dependency = default
    ) =>
      new GenUniformGrid3DJob
      {
        noise = noise,
        texture = texture,
        seed = seed,
        offset = offset,
        stepSize = stepSize,
        outputMinMax = outputMinMax,
      }.Schedule(dependency);
  }
}
