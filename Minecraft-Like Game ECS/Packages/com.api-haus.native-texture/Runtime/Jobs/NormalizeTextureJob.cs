namespace NativeTexture.Jobs
{
  using Unity.Burst;
  using Unity.Collections;
  using Unity.Collections.LowLevel.Unsafe;
  using Unity.Jobs;

  /// <summary>
  /// Burst-compiled parallel job that normalizes texture values from [min, max] to [0, 1].
  /// </summary>
  [BurstCompile]
  public struct NormalizeTextureJob : IJobParallelFor
  {
    [NativeMatchesParallelForLength]
    private NativeArray<float> m_Texture;

    private float m_Min;
    private float m_Scale;

    [BurstCompile]
    public void Execute(int i) => m_Texture[i] = (m_Texture[i] - m_Min) * m_Scale;

    public static JobHandle Schedule(
      NativeTexture2D<float> tex,
      float min,
      float max,
      JobHandle dependency = default
    )
    {
      float range = max - min;
      return new NormalizeTextureJob
      {
        m_Texture = tex.AsDeferredJobArray(),
        m_Min = min,
        m_Scale = range != 0f ? 1f / range : 0f,
      }.Schedule(tex.Length, 64, dependency);
    }

    public static JobHandle Schedule(
      NativeTexture3D<float> tex,
      float min,
      float max,
      JobHandle dependency = default
    )
    {
      float range = max - min;
      return new NormalizeTextureJob
      {
        m_Texture = tex.AsDeferredJobArray(),
        m_Min = min,
        m_Scale = range != 0f ? 1f / range : 0f,
      }.Schedule(tex.Length, 64, dependency);
    }
  }
}
