namespace NativeTexture.FastNoise2.Tests
{
  using global::FastNoise2.Bindings;
  using NativeTexture.Jobs;
  using NUnit.Framework;
  using Unity.Collections;
  using Unity.Jobs;
  using Unity.Mathematics;
  using UnityEngine;
  using static global::FastNoise2.Bindings.FastNoise;

  [TestFixture]
  public class NoiseGeneration2DTests
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
    public void SyncGenUniformGrid2D()
    {
      const int w = 32, h = 32;
      var nt = new NativeTexture2D<float>(new int2(w, h), Allocator.TempJob);
      try
      {
        m_Noise.GenUniformGrid2D(nt, out OutputMinMax minMax, 0, 0, w, h, 0.02f, 0.02f, 1337);

        Assert.Less(minMax.min, minMax.max, "OutputMinMax should have min < max");
        Assert.AreEqual(w * h, nt.Length);

        // Verify at least some values are non-zero (noise was generated)
        bool hasNonZero = false;
        for (int i = 0; i < nt.Length; i++)
        {
          if (math.abs(nt[i]) > 1e-6f)
          {
            hasNonZero = true;
            break;
          }
        }
        Assert.IsTrue(hasNonZero, "Noise output should contain non-zero values");
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void SyncGenTileable2D()
    {
      const int w = 16, h = 16;
      var nt = new NativeTexture2D<float>(new int2(w, h), Allocator.TempJob);
      try
      {
        m_Noise.GenTileable2D(nt, out OutputMinMax minMax, w, h, 0.02f, 0.02f, 42);

        Assert.Less(minMax.min, minMax.max, "OutputMinMax should have min < max");
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void JobGenUniformGrid2D()
    {
      const int w = 32, h = 32;
      var nt = new NativeTexture2D<float>(new int2(w, h), Allocator.TempJob);
      var minMaxRef = new NativeReference<OutputMinMax>(Allocator.TempJob);
      try
      {
        var handle = m_Noise.GenUniformGrid2D(nt, minMaxRef, 1337, float2.zero, new float2(0.02f), default);
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
    public void JobGenUniformGrid2D_WithNormalize()
    {
      const int w = 32, h = 32;
      var nt = new NativeTexture2D<float>(new int2(w, h), Allocator.TempJob);
      var minMaxRef = new NativeReference<OutputMinMax>(Allocator.TempJob);
      try
      {
        var genHandle = m_Noise.GenUniformGrid2D(nt, minMaxRef, 1337, float2.zero, new float2(0.02f), default);
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

    [Test]
    public void ChainedNoiseAndNormalize()
    {
      const int w = 16, h = 16;
      var nt = new NativeTexture2D<float>(new int2(w, h), Allocator.TempJob);
      var minMaxRef = new NativeReference<OutputMinMax>(Allocator.TempJob);
      try
      {
        // Chain: generate noise -> normalize, using job dependencies
        var genHandle = m_Noise.GenUniformGrid2D(nt, minMaxRef, 42, float2.zero, new float2(0.05f), default);
        genHandle.Complete();

        var normHandle = nt.ScheduleNormalize(minMaxRef.Value, default);
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

    [Test]
    public void ZeroCopyTexture2D()
    {
      var tex = new Texture2D(16, 16, TextureFormat.RFloat, false);
      try
      {
        var nt = new NativeTexture2D<float>(tex);
        m_Noise.GenUniformGrid2D(nt, out OutputMinMax minMax, 0, 0, 16, 16, 0.02f, 0.02f, 99);

        // Write back to Texture2D
        nt.ApplyTo(tex);

        // Read back via fresh NativeTexture wrapping same Texture2D
        var readback = new NativeTexture2D<float>(tex);
        bool hasNonZero = false;
        for (int i = 0; i < readback.Length; i++)
        {
          if (math.abs(readback[i]) > 1e-6f)
          {
            hasNonZero = true;
            break;
          }
        }
        Assert.IsTrue(hasNonZero, "Round-trip should preserve non-zero noise data");
      }
      finally
      {
        Object.DestroyImmediate(tex);
      }
    }
  }
}
