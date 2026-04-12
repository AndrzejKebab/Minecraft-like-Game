namespace NativeTexture.Tests
{
  using System;
  using NUnit.Framework;
  using Unity.Collections;
  using Unity.Jobs;
  using Unity.Mathematics;
  using UnityEngine;

  [TestFixture]
  public class NativeTexture2DTests
  {
    [Test]
    public void CreateFromTexture2D()
    {
      var tex = new Texture2D(16, 8, TextureFormat.RFloat, false);
      try
      {
        var nt = new NativeTexture2D<float>(tex);
        Assert.IsTrue(nt.IsCreated);
        Assert.AreEqual(16, nt.Width);
        Assert.AreEqual(8, nt.Height);
        Assert.AreEqual(new int2(16, 8), nt.Resolution);
        Assert.AreEqual(16 * 8, nt.Length);
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(tex);
      }
    }

    [Test]
    public void CreateWithAllocator()
    {
      var nt = new NativeTexture2D<float>(new int2(32, 16), Allocator.TempJob);
      try
      {
        Assert.IsTrue(nt.IsCreated);
        Assert.AreEqual(32, nt.Width);
        Assert.AreEqual(16, nt.Height);
        Assert.AreEqual(32 * 16, nt.Length);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void DataAccess_LinearIndex()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        nt[0] = 1.5f;
        nt[5] = 2.5f;
        nt[15] = 3.5f;

        Assert.AreEqual(1.5f, nt[0]);
        Assert.AreEqual(2.5f, nt[5]);
        Assert.AreEqual(3.5f, nt[15]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void DataAccess_CoordIndex()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        nt[new int2(0, 0)] = 10f;
        nt[new int2(3, 2)] = 20f;

        Assert.AreEqual(10f, nt[new int2(0, 0)]);
        Assert.AreEqual(20f, nt[new int2(3, 2)]);

        // Verify coord indexer matches linear: index = y * width + x
        Assert.AreEqual(20f, nt[2 * 4 + 3]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void AsArray()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        nt[0] = 42f;
        var arr = nt.AsArray();

        Assert.AreEqual(16, arr.Length);
        Assert.AreEqual(42f, arr[0]);

        // Array is a view - writes are reflected
        arr[1] = 99f;
        Assert.AreEqual(99f, nt[1]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void ReadOnlyView()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        nt[0] = 7f;
        nt[new int2(1, 2)] = 13f;

        var ro = nt.AsReadOnly();
        Assert.IsTrue(ro.IsCreated);
        Assert.AreEqual(4, ro.Width);
        Assert.AreEqual(4, ro.Height);
        Assert.AreEqual(16, ro.Length);
        Assert.AreEqual(7f, ro[0]);
        Assert.AreEqual(13f, ro[new int2(1, 2)]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void Dispose_ReleasesMemory()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      Assert.IsTrue(nt.IsCreated);

      nt.Dispose();
      Assert.IsFalse(nt.IsCreated);
    }

    [Test]
    public void DisposeJobHandle()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      Assert.IsTrue(nt.IsCreated);

      var handle = nt.Dispose(default);
      handle.Complete();

      Assert.IsFalse(nt.IsCreated);
    }

    [Test]
    public void ApplyToTexture2D()
    {
      var tex = new Texture2D(4, 4, TextureFormat.RFloat, false);
      try
      {
        var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
        try
        {
          for (int i = 0; i < 16; i++)
            nt[i] = i * 0.1f;

          nt.ApplyTo(tex);

          // Read back via a fresh NativeTexture wrapping the same Texture2D
          var readback = new NativeTexture2D<float>(tex);
          for (int i = 0; i < 16; i++)
            Assert.AreEqual(i * 0.1f, readback[i], 1e-5f, $"Mismatch at index {i}");
        }
        finally
        {
          nt.Dispose();
        }
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(tex);
      }
    }

    struct WriteJob : IJobParallelFor
    {
      public NativeTexture2D<float> texture;

      public void Execute(int index)
      {
        texture[index] = index * 2f;
      }
    }

    [Test]
    public void JobSystemIntegration()
    {
      var nt = new NativeTexture2D<float>(new int2(8, 8), Allocator.TempJob);
      try
      {
        new WriteJob { texture = nt }.Schedule(nt.Length, 16).Complete();

        for (int i = 0; i < nt.Length; i++)
          Assert.AreEqual(i * 2f, nt[i]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    struct DeferredWriteJob : IJobParallelFor
    {
      [NativeDisableParallelForRestriction]
      public NativeArray<float> data;

      public void Execute(int index)
      {
        data[index] = index + 1f;
      }
    }

    [Test]
    public void DeferredJobArray()
    {
      var nt = new NativeTexture2D<float>(new int2(4, 4), Allocator.TempJob);
      try
      {
        var deferred = nt.AsDeferredJobArray();
        var handle = new DeferredWriteJob { data = deferred }.Schedule(nt.Length, 8);
        handle.Complete();

        for (int i = 0; i < nt.Length; i++)
          Assert.AreEqual(i + 1f, nt[i]);
      }
      finally
      {
        nt.Dispose();
      }
    }
  }
}
