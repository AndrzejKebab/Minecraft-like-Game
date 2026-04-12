namespace NativeTexture.Tests
{
  using NUnit.Framework;
  using Unity.Collections;
  using Unity.Collections.LowLevel.Unsafe;
  using Unity.Jobs;
  using Unity.Mathematics;

  [TestFixture]
  public class NativeTexture4DTests
  {
    [Test]
    public void CreateWithAllocator()
    {
      var nt = new NativeTexture4D<float>(new int4(8, 4, 2, 3), Allocator.TempJob);
      try
      {
        Assert.IsTrue(nt.IsCreated);
        Assert.AreEqual(8, nt.Width);
        Assert.AreEqual(4, nt.Height);
        Assert.AreEqual(2, nt.Depth);
        Assert.AreEqual(3, nt.WSize);
        Assert.AreEqual(new int4(8, 4, 2, 3), nt.Resolution);
        Assert.AreEqual(8 * 4 * 2 * 3, nt.Length);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void DataAccess_LinearIndex()
    {
      var nt = new NativeTexture4D<float>(new int4(4, 4, 4, 2), Allocator.TempJob);
      try
      {
        nt[0] = 1.5f;
        nt[10] = 2.5f;
        nt[127] = 3.5f;

        Assert.AreEqual(1.5f, nt[0]);
        Assert.AreEqual(2.5f, nt[10]);
        Assert.AreEqual(3.5f, nt[127]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void DataAccess_CoordIndex()
    {
      var nt = new NativeTexture4D<float>(new int4(4, 4, 4, 2), Allocator.TempJob);
      try
      {
        nt[new int4(0, 0, 0, 0)] = 10f;
        nt[new int4(3, 2, 1, 1)] = 20f;

        Assert.AreEqual(10f, nt[new int4(0, 0, 0, 0)]);
        Assert.AreEqual(20f, nt[new int4(3, 2, 1, 1)]);

        // Verify: index = w * (w*h*d) + z * (w*h) + y * w + x
        //       = 1 * (4*4*4) + 1 * (4*4) + 2 * 4 + 3 = 64 + 16 + 8 + 3 = 91
        Assert.AreEqual(20f, nt[91]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void AsArray()
    {
      var nt = new NativeTexture4D<float>(new int4(4, 4, 2, 2), Allocator.TempJob);
      try
      {
        nt[0] = 42f;
        var arr = nt.AsArray();

        Assert.AreEqual(64, arr.Length);
        Assert.AreEqual(42f, arr[0]);

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
      var nt = new NativeTexture4D<float>(new int4(4, 4, 2, 2), Allocator.TempJob);
      try
      {
        nt[0] = 7f;
        nt[new int4(1, 2, 1, 1)] = 13f;

        var ro = nt.AsReadOnly();
        Assert.IsTrue(ro.IsCreated);
        Assert.AreEqual(7f, ro[0]);
        Assert.AreEqual(13f, ro[new int4(1, 2, 1, 1)]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public void Dispose_ReleasesMemory()
    {
      var nt = new NativeTexture4D<float>(new int4(4, 4, 2, 2), Allocator.TempJob);
      Assert.IsTrue(nt.IsCreated);

      nt.Dispose();
      Assert.IsFalse(nt.IsCreated);
    }

    struct WriteJob4D : IJobParallelFor
    {
      public NativeTexture4D<float> texture;

      public void Execute(int index)
      {
        texture[index] = index * 3f;
      }
    }

    [Test]
    public void JobSystemIntegration()
    {
      var nt = new NativeTexture4D<float>(new int4(4, 4, 4, 2), Allocator.TempJob);
      try
      {
        new WriteJob4D { texture = nt }.Schedule(nt.Length, 16).Complete();

        for (int i = 0; i < nt.Length; i++)
          Assert.AreEqual(i * 3f, nt[i]);
      }
      finally
      {
        nt.Dispose();
      }
    }

    [Test]
    public unsafe void UnsafePointerAccess()
    {
      var nt = new NativeTexture4D<float>(new int4(4, 4, 2, 2), Allocator.TempJob);
      try
      {
        float* ptr = (float*)nt.GetUnsafePtr();
        Assert.IsTrue(ptr != null);

        ptr[0] = 100f;
        ptr[63] = 200f;

        Assert.AreEqual(100f, nt[0]);
        Assert.AreEqual(200f, nt[63]);
      }
      finally
      {
        nt.Dispose();
      }
    }
  }
}
