using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;

namespace FastNoise2.Tests
{
	using System.Collections.Generic;
	using Bindings;
	using Generators;

	public class BindingTests
	{
		[Test]
		public void GenerateBitmapTestSafe()
		{
			using FastNoise cellular = new("CellularDistance");
			cellular.Set("ReturnType", "Index0Add1");
			cellular.Set("DistanceIndex0", 2);

			using FastNoise fractal = new("FractalFBm");
			fractal.Set("Source", new FastNoise("Simplex"));
			fractal.Set("Gain", 0.3f);
			fractal.Set("Lacunarity", 0.6f);

			using FastNoise addDim = new("AddDimension");
			addDim.Set("Source", cellular);
			addDim.Set("NewDimensionPosition", 0.5f);
			// or
			// addDim.Set("NewDimensionPosition", new FastNoise("Perlin"));

			using FastNoise maxSmooth = new("MaxSmooth");
			maxSmooth.Set("LHS", fractal);
			maxSmooth.Set("RHS", addDim);

			Debug.Log("SIMD Level " + maxSmooth.GetSIMDLevel());

			GenerateBitmap(maxSmooth, "testMetadata");

			// Dunes
			using FastNoise nodeTree = FastNoise.FromEncodedNodeTree(
				"E@BBZEG@BD8JFgIECArXIzwECiQIw/UoPwkuAAE@BJDQAH@BC@AIEAJBw@ABZEED0KV78YZmZmPwQDmpkZPwsAAIA/HAMAAHBCBA=="
			);

			// Encoded node trees can be invalid and return null
			if (nodeTree != FastNoise.Invalid)
				GenerateBitmap(nodeTree, "testENT");
		}

		[Test]
		public void NewNodeTypes()
		{
			// Verify new node types can be constructed
			using FastNoise pingPong = new("PingPong");
			Assert.That(pingPong.IsCreated, Is.True);

			using FastNoise abs = new("Abs");
			Assert.That(abs.IsCreated, Is.True);

			using FastNoise signedSqrt = new("SignedSquareRoot");
			Assert.That(signedSqrt.IsCreated, Is.True);

			using FastNoise domainRotatePlane = new("DomainRotatePlane");
			Assert.That(domainRotatePlane.IsCreated, Is.True);

			using FastNoise domainWarpSimplex = new("DomainWarpSimplex");
			Assert.That(domainWarpSimplex.IsCreated, Is.True);

			using FastNoise domainWarpSuperSimplex = new("DomainWarpSuperSimplex");
			Assert.That(domainWarpSuperSimplex.IsCreated, Is.True);

			using FastNoise modulus = new("Modulus");
			Assert.That(modulus.IsCreated, Is.True);
		}

		[Test]
		public void RenamedNodeTypes()
		{
			// SuperSimplex (was OpenSimplex2) should work
			using FastNoise superSimplex = new("SuperSimplex");
			Assert.That(superSimplex.IsCreated, Is.True);

			// Gradient (was PositionOutput) should work
			using FastNoise gradient = new("Gradient");
			Assert.That(gradient.IsCreated, Is.True);

			// Old names should throw
			Assert.Throws<ArgumentException>(() =>
			{
				using FastNoise _ = new("OpenSimplex2");
			});
			Assert.Throws<ArgumentException>(() =>
			{
				using FastNoise _ = new("PositionOutput");
			});
		}

		[Test]
		public void HybridParameters()
		{
			// Terrace.Smoothness accepts both float and generator
			using FastNoise terrace = new("Terrace");
			terrace.Set("Source", new FastNoise("Simplex"));
			terrace.Set("Smoothness", 0.5f);

			using FastNoise terraceGen = new("Terrace");
			terraceGen.Set("Source", new FastNoise("Simplex"));
			terraceGen.Set("Smoothness", new FastNoise("Perlin"));

			// CellularDistance.SizeJitter accepts generator
			using FastNoise cell = new("CellularDistance");
			cell.Set("SizeJitter", new FastNoise("Simplex"));
		}

		[Test]
		public void RenamedFields()
		{
			// DomainScale.Scaling (was Scale)
			using FastNoise domainScale = new("DomainScale");
			domainScale.Set("Source", new FastNoise("Simplex"));
			domainScale.Set("Scaling", 2.0f);

			// Terrace.StepCount (was Multiplier)
			using FastNoise terrace = new("Terrace");
			terrace.Set("Source", new FastNoise("Simplex"));
			terrace.Set("StepCount", 4.0f);

			// Cellular.GridJitter (was JitterModifier)
			using FastNoise cellular = new("CellularDistance");
			cellular.Set("GridJitter", 0.5f);
		}

		[Test]
		public void MetadataIntrospection()
		{
			// AllNodeNames should contain expected entries
			string[] nodeNames = FN2NodeRegistry.AllNodeNames;
			Assert.That(nodeNames.Length, Is.GreaterThan(0));
			Assert.That(nodeNames, Does.Contain("Simplex"));
			Assert.That(nodeNames, Does.Contain("CellularDistance"));
			Assert.That(nodeNames, Does.Contain("FractalFBm"));

			// GetNodeDef returns valid data
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef("Simplex");
			Assert.That(def, Is.Not.Null);
			Assert.That(def.Members, Is.Not.Null);

			// Hybrid members on FractalFBm
			FN2NodeDef fbmDef = FN2NodeRegistry.GetNodeDef("FractalFBm");
			var fbmHybrids = fbmDef.GetMembersOfType(FN2MemberType.Hybrid);
			Assert.That(fbmHybrids.Count, Is.GreaterThan(0));
			Assert.That(fbmHybrids.TrueForAll(m => m.Type == FN2MemberType.Hybrid), Is.True);

			// Invalid name throws
			Assert.Throws<ArgumentException>(() => FN2NodeRegistry.GetNodeDef("NonExistentNode"));
		}

		private static void GenerateBitmap(FastNoise fastNoise, string filename, ushort size = 512)
		{
			using (BinaryWriter writer = new(File.Open(filename + ".bmp", FileMode.Create)))
			{
				const uint imageDataOffset = 14u + 12u + (256u * 3u);

				// File header (14)
				writer.Write('B');
				writer.Write('M');
				writer.Write(imageDataOffset + (uint)(size * size)); // file size
				writer.Write(0); // reserved
				writer.Write(imageDataOffset); // image data offset
				// Bmp Info Header (12)
				writer.Write(12u); // size of header
				writer.Write(size); // width
				writer.Write(size); // height
				writer.Write((ushort)1); // color planes
				writer.Write((ushort)8); // bit depth
				// Colour map
				for (int i = 0; i < 256; i++)
				{
					writer.Write((byte)i);
					writer.Write((byte)i);
					writer.Write((byte)i);
				}

				// Image data
				float[] noiseData = new float[size * size];
				FastNoise.OutputMinMax minMax = fastNoise.GenUniformGrid2D(
					noiseData,
					0f,
					0f,
					size,
					size,
					0.02f,
					0.02f,
					1337
				);

				float scale = 255.0f / (minMax.max - minMax.min);

				foreach (float noise in noiseData)
				{
					//Scale noise to 0 - 255
					int noiseI = (int)round((noise - minMax.min) * scale);

					writer.Write((byte)clamp(noiseI, 0, 255));
				}
			}
		}
	}
}
