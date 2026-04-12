namespace NativeTexture.Tests
{
  using NativeTexture.Jobs;
  using NUnit.Framework;
  using Unity.Collections;
  using Unity.Mathematics;

  [TestFixture]
  public class NormalizeJobTests
  {
    [Test]
    public void Normalize2D_ZeroToOne()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        // Fill with values in [-5, 10] range
        for (int i = 0; i < nt.Length; i++)
          nt[i] = -5f + i;

        NormalizeTextureJob.Schedule(nt, -5f, 10f).Complete();

        for (int i = 0; i < nt.Length; i++)
        {
          float val = nt[i];
          Assert.GreaterOrEqual(val, -1e-5f, $"Index {i} below 0: {val}");
          Assert.LessOrEqual(val, 1f + 1e-5f, $"Index {i} above 1: {val}");
        }

        // Check endpoints
        Assert.AreEqual(0f, nt[0], 1e-5f); // (-5 - (-5)) / 15 = 0
        Assert.AreEqual(1f, nt[15], 1e-5f); // (10 - (-5)) / 15 = 1
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void Normalize3D_ZeroToOne()
    {
      var nt = new NativeTexture3D<float>(new int3(4, 4, 2), Allocator.TempJob);
      try
      {
        for (int i = 0; i < nt.Length; i++)
          nt[i] = i * 0.5f;

        float min = 0f;
        float max = (nt.Length - 1) * 0.5f;
        NormalizeTextureJob.Schedule(nt, min, max).Complete();

        for (int i = 0; i < nt.Length; i++)
        {
          float val = nt[i];
          Assert.GreaterOrEqual(val, -1e-5f, $"Index {i} below 0: {val}");
          Assert.LessOrEqual(val, 1f + 1e-5f, $"Index {i} above 1: {val}");
        }

        Assert.AreEqual(0f, nt[0], 1e-5f);
        Assert.AreEqual(1f, nt[nt.Length - 1], 1e-5f);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void Normalize_ZeroRange()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        for (int i = 0; i < nt.Length; i++)
          nt[i] = 5f;

        // min == max, should not divide by zero; scale is 0 so result = (v - min) * 0 = 0
        NormalizeTextureJob.Schedule(nt, 5f, 5f).Complete();

        for (int i = 0; i < nt.Length; i++)
          Assert.AreEqual(0f, nt[i], 1e-5f, $"Index {i} should be 0 when range is zero");
      }
      finally
      {
        nt.Dispose();
      }
    }
  }
}
