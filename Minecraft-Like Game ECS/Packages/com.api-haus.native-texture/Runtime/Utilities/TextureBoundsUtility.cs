namespace NativeTexture.Utilities
{
  using Jobs;
  using Unity.Jobs;

  /// <summary>
  /// Utility methods for normalizing texture data.
  /// </summary>
  public static class TextureBoundsUtility
  {
    /// <summary>
    /// Schedules a normalization job that maps values from [min, max] to [0, 1].
    /// </summary>
    public static JobHandle ScheduleNormalize(
      this NativeTexture2D<float> texture,
      float min,
      float max,
      JobHandle dependency = default
    ) => NormalizeTextureJob.Schedule(texture, min, max, dependency);

    /// <summary>
    /// Schedules a normalization job that maps values from [min, max] to [0, 1].
    /// </summary>
    public static JobHandle ScheduleNormalize(
      this NativeTexture3D<float> texture,
      float min,
      float max,
      JobHandle dependency = default
    ) => NormalizeTextureJob.Schedule(texture, min, max, dependency);
  }
}
