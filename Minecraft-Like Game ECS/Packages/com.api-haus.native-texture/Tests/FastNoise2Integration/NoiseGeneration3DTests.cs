namespace NativeTexture.FastNoise2.Tests
{
  using global::FastNoise2.Bindings;
  using NUnit.Framework;
  using Unity.Collections;
  using Unity.Jobs;
  using Unity.Mathematics;
  using static global::FastNoise2.Bindings.FastNoise;

  [TestFixture]
  public class NoiseGeneration3DTests
  {
    FastNoise m_Noise;

    [SetUp]
    public void SetUp()
    {
      m_Noise = new FastNoise("Simplex");
      Assert.That(m_Noise.IsCreated, Is.True, "Failed to create Simplex noise node");
    }

    [TearDown]
    public void TearDown()
    {
      if (m_Noise.IsCreated) m_Noise.Dispose();
    }

    [Test]
    public void SyncGenUniformGrid3D()
    {
      const int w = 8, h = 8, d = 8;
      var nt = new NativeTexture3D<float>(new int3(w, h, d), Allocator.TempJob);
      try
      {
        m_Noise.GenUniformGrid3D(nt, out OutputMinMax minMax, 0, 0, 0, w, h, d, 0.05f, 0.05f, 0.05f, 1337);

        Assert.Less(minMax.min, minMax.max, "OutputMinMax should have min < max");
        Assert.AreEqual(w * h * d, nt.Length);

        bool hasNonZero = false;
        for (int i = 0; i < nt.Length; i++)
        {
          if (math.abs(nt[i]) > 1e-6f)
          {
            hasNonZero = true;
            break;
          }
        }
        Assert.IsTrue(hasNonZero, "3D noise output should contain non-zero values");
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void JobGenUniformGrid3D()
    {
      const int w = 8, h = 8, d = 8;
      var nt = new NativeTexture3D<float>(new int3(w, h, d), Allocator.TempJob);
      var minMaxRef = new NativeReference<OutputMinMax>(Allocator.TempJob);
      try
      {
        var handle = m_Noise.GenUniformGrid3D(nt, minMaxRef, 1337, float3.zero, new float3(0.05f), default);
        handle.Complete();

        var minMax = minMaxRef.Value;
        Assert.Less(minMax.min, minMax.max, "OutputMinMax should have min < max");
      }
      finally
      {
        minMaxRef.Dispose();
        nt.Dispose();
      }
    }

    [Test]
    public void JobGenUniformGrid3D_WithNormalize()
    {
      const int w = 8, h = 8, d = 8;
      var nt = new NativeTexture3D<float>(new int3(w, h, d), Allocator.TempJob);
      var minMaxRef = new NativeReference<OutputMinMax>(Allocator.TempJob);
      try
      {
        var genHandle = m_Noise.GenUniformGrid3D(nt, minMaxRef, 1337, float3.zero, new float3(0.05f), default);
        genHandle.Complete();

        var normHandle = nt.ScheduleNormalize(minMaxRef.Value);
        normHandle.Complete();

        for (int i = 0; i < nt.Length; i++)
        {
          float val = nt[i];
          Assert.GreaterOrEqual(val, -1e-5f, $"Index {i} below 0: {val}");
          Assert.LessOrEqual(val, 1f + 1e-5f, $"Index {i} above 1: {val}");
        }
      }
      finally
      {
        minMaxRef.Dispose();
        nt.Dispose();
      }
    }
  }
}
