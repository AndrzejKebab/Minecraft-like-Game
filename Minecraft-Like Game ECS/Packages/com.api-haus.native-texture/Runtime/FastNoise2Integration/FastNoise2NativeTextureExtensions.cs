namespace NativeTexture.FastNoise2
{
  using global::FastNoise2.Bindings;
  using static global::FastNoise2.Bindings.FastNoise;

  /// <summary>
  /// Synchronous extension methods that bridge NativeTexture containers with FastNoise2 generation.
  /// These call the native library directly and block until complete.
  /// </summary>
  public static class FastNoise2NativeTextureExtensions
  {
    public static unsafe void GenUniformGrid2D<T>(
      this FastNoise fn,
      NativeTexture2D<T> nativeTexture,
      out OutputMinMax minMax,
      float xOffset,
      float yOffset,
      int xCount,
      int yCount,
      float xStepSize,
      float yStepSize,
      int seed
    )
      where T : unmanaged
    {
      OutputMinMax local = default;
      fn.GenUniformGrid2D(
        nativeTexture.GetUnsafePtr(),
        &local,
        xOffset,
        yOffset,
        xCount,
        yCount,
        xStepSize,
        yStepSize,
        seed
      );
      minMax = local;
    }

    public static unsafe void GenUniformGrid3D<T>(
      this FastNoise fn,
      NativeTexture3D<T> nativeTexture,
      out OutputMinMax minMax,
      float xOffset,
      float yOffset,
      float zOffset,
      int xCount,
      int yCount,
      int zCount,
      float xStepSize,
      float yStepSize,
      float zStepSize,
      int seed
    )
      where T : unmanaged
    {
      OutputMinMax local = default;
      fn.GenUniformGrid3D(
        nativeTexture.GetUnsafePtr(),
        &local,
        xOffset,
        yOffset,
        zOffset,
        xCount,
        yCount,
        zCount,
        xStepSize,
        yStepSize,
        zStepSize,
        seed
      );
      minMax = local;
    }

    public static unsafe void GenTileable2D<T>(
      this FastNoise fn,
      NativeTexture2D<T> nativeTexture,
      out OutputMinMax minMax,
      int xSize,
      int ySize,
      float xStepSize,
      float yStepSize,
      int seed
    )
      where T : unmanaged
    {
      OutputMinMax local = default;
      fn.GenTileable2D(
        nativeTexture.GetUnsafePtr(),
        &local,
        xSize,
        ySize,
        xStepSize,
        yStepSize,
        seed
      );
      minMax = local;
    }
  }
}
