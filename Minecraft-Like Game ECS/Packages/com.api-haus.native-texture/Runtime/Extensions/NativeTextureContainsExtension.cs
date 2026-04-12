namespace NativeTexture.Extensions
{
  using Unity.Mathematics;
  using Utilities;

  public static class NativeTextureContainsExtension
  {
    public static bool Contains<T>(this NativeTexture2D<T> t, int2 coord)
      where T : unmanaged
    {
      int i = coord.ToIndex(t.Width);
      return i >= 0 && i < t.Length;
    }
  }
}
