namespace NativeTexture.FastNoise2
{
  using global::FastNoise2.Bindings;
  using NativeTexture.Jobs;
  using Unity.Jobs;

  /// <summary>
  /// Extension methods that bridge FastNoise2's OutputMinMax to the core NormalizeTextureJob.
  /// </summary>
  public static class NativeTextureNormalizeFN2Ext
  {
    /// <summary>
    /// Schedules normalization using FastNoise2 OutputMinMax bounds.
    /// </summary>
    public static JobHandle ScheduleNormalize(
      this NativeTexture2D<float> texture,
      FastNoise.OutputMinMax minMax,
      JobHandle dependency = default
    ) => NormalizeTextureJob.Schedule(texture, minMax.min, minMax.max, dependency);

    /// <summary>
    /// Schedules normalization using FastNoise2 OutputMinMax bounds.
    /// </summary>
    public static JobHandle ScheduleNormalize(
      this NativeTexture3D<float> texture,
      FastNoise.OutputMinMax minMax,
      JobHandle dependency = default
    ) => NormalizeTextureJob.Schedule(texture, minMax.min, minMax.max, dependency);
  }
}
