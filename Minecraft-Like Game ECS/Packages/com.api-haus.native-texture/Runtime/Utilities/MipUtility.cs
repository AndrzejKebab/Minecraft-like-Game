namespace NativeTexture.Utilities
{
  using Unity.Mathematics;
  using static Unity.Mathematics.math;

  public static class MipUtility
  {
    public static int MipCount(int size) => (int)(1 + floor(log2(size)));

    public static int MipCount(int3 size) => MipCount(cmax(size));

    public static int MipCount(int2 size) => MipCount(cmax(size));

    public static int TextureSize(int size, int mip) => size / (1 << mip);

    public static int2 TextureSize(int2 size, int mip) => size / (1 << mip);

    public static int TexelLength(in int2 resolution, in int mipCount)
    {
      int len = 0;

      for (int level = 0; level < mipCount; level++)
      {
        int2 r = resolution / (1 << level);
        len += r.x * r.y;
      }

      return len;
    }
  }
}
