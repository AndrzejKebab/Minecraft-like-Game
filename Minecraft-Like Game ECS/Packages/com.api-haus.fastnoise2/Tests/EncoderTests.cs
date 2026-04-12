using NUnit.Framework;

namespace FastNoise2.Tests
{
	using Bindings;
	using Generators;

	public class EncoderTests
	{
		#region Encode → C++ Decode

		[Test]
		public void EncodeSimplexToCppDecode()
		{
			NoiseNode node = Noise.Simplex();
			string encoded = node.Encode();
			Assert.That(encoded, Is.Not.Null.And.Not.Empty);

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);
			Assert.That(fn.IsCreated, Is.True);

			float val = fn.GenSingle2D(1f, 1f, 1337);
			Assert.That(val, Is.Not.NaN);
		}

		[Test]
		public void EncodeConstantToCppDecode()
		{
			NoiseNode node = Noise.Constant(0.5f);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);
			Assert.That(fn.GenSingle2D(0f, 0f, 1337), Is.EqualTo(0.5f).Within(0.001f));
		}

		[Test]
		public void EncodeFbmChainToCppDecode()
		{
			NoiseNode node = Noise.Simplex().Fbm(0.5f, 0f, 3, 2f);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);
			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[64 * 64];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, 64, 64, 1f, 1f, 1337);
			Assert.That(minMax.max, Is.GreaterThan(minMax.min));
		}

		[Test]
		public void EncodeDomainWarpToCppDecode()
		{
			NoiseNode node = Noise.Simplex()
				.DomainWarpGradient(0.5f, 2f)
				.DomainWarpProgressive(0.5f, 0f, 3, 2f);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);

			float[] data = new float[32 * 32];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, 32, 32, 1f, 1f, 1337);
			Assert.That(minMax.max, Is.GreaterThan(minMax.min));
		}

		[Test]
		public void EncodeComplexChainToCppDecode()
		{
			NoiseNode node = Noise.Simplex()
				.Fbm(0.5f, 0f, 4, 2f)
				.DomainScale(0.5f)
				.Abs()
				.Terrace(8f, 0.3f)
				.Remap(-1f, 1f, 0f, 1f);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);

			float[] data = new float[32 * 32];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, 32, 32, 1f, 1f, 1337);
			Assert.That(minMax.max, Is.GreaterThan(minMax.min));
		}

		[Test]
		public void EncodeGradientNodeToCppDecode()
		{
			NoiseNode node = Noise.Gradient()
				.WithMultipliers(0f, 3f, 0f, 0f)
				.WithOffsets(0f, 0f, 0f, 0f);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);
		}

		[Test]
		public void EncodeCellularDistanceToCppDecode()
		{
			NoiseNode node = Noise.CellularDistance()
				.WithDistanceFunction(DistanceFunction.Manhattan)
				.WithReturnType(CellularReturnType.Index0Add1);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);
		}

		[Test]
		public void EncodeOperatorsToCppDecode()
		{
			// Add (LHS=NodeLookup, RHS=Hybrid)
			string encoded = (Noise.Simplex() + 1f).Encode();
			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "Add failed: " + encoded);

			// Multiply
			encoded = (Noise.Simplex() * 2f).Encode();
			using FastNoise fn2 = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn2 != FastNoise.Invalid, Is.True, "Multiply failed: " + encoded);

			// Subtract
			encoded = (Noise.Simplex() - 0.5f).Encode();
			using FastNoise fn3 = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn3 != FastNoise.Invalid, Is.True, "Subtract failed: " + encoded);
		}

		[Test]
		public void EncodeHybridNodeToCppDecode()
		{
			NoiseNode node = Noise.Simplex().Fbm(Noise.Perlin(), 0f, 3, 2f);
			string encoded = node.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True, "C++ failed to decode: " + encoded);
		}

		#endregion

		#region C++ Encode → C# Decode

		[Test]
		public void CppEncodedDunes_DecodeAndBuild()
		{
			// Known encoded string from BindingTests (Dunes preset)
			string encoded =
				"E@BBZEG@BD8JFgIECArXIzwECiQIw/UoPwkuAAE@BJDQAH@BC@AIEAJBw@ABZEED0KV78YZmZmPwQDmpkZPwsAAIA/HAMAAHBCBA==";

			// Verify C++ can decode it
			using FastNoise cppNode = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(cppNode != FastNoise.Invalid, Is.True, "C++ can't decode known string");

			// Verify C# can decode and build
			NoiseNode decoded = NoiseNode.Decode(encoded);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Descriptor, Is.Not.Null);
			Assert.That(decoded.Descriptor.NodeName, Is.Not.Null.And.Not.Empty);

			using FastNoise fn = decoded.Build();
			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[64 * 64];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, 64, 64, 1f, 1f, 1337);
			Assert.That(minMax.max, Is.GreaterThan(minMax.min));
		}

		#endregion

		#region Full Round-Trip: Encode → Decode → Build

		static void AssertRoundTripProducesNoise(NoiseNode node, int size = 32)
		{
			string encoded = node.Encode();
			Assert.That(encoded, Is.Not.Null.And.Not.Empty,
				"Encode returned empty for " + node.Descriptor.NodeName);

			NoiseNode decoded = NoiseNode.Decode(encoded);
			Assert.That(decoded, Is.Not.Null, "Decode returned null for: " + encoded);

			using FastNoise fn = decoded.Build();
			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[size * size];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, size, size, 1f, 1f, 1337);
			Assert.That(minMax.min, Is.Not.NaN);
			Assert.That(minMax.max, Is.Not.NaN);
		}

		[Test]
		public void RoundTrip_Simplex() =>
			AssertRoundTripProducesNoise(Noise.Simplex());

		[Test]
		public void RoundTrip_Constant() =>
			AssertRoundTripProducesNoise(Noise.Constant(0.5f));

		[Test]
		public void RoundTrip_Checkerboard() =>
			AssertRoundTripProducesNoise(Noise.Checkerboard());

		[Test]
		public void RoundTrip_White() =>
			AssertRoundTripProducesNoise(Noise.White());

		[Test]
		public void RoundTrip_Perlin() =>
			AssertRoundTripProducesNoise(Noise.Perlin());

		[Test]
		public void RoundTrip_FbmChain() =>
			AssertRoundTripProducesNoise(Noise.Simplex().Fbm(0.5f, 0f, 4, 2f));

		[Test]
		public void RoundTrip_DomainWarp() =>
			AssertRoundTripProducesNoise(
				Noise.Simplex().DomainWarpGradient(0.5f, 2f)
					.DomainWarpProgressive(0.5f, 0f, 3, 2f));

		[Test]
		public void RoundTrip_Gradient() =>
			AssertRoundTripProducesNoise(
				Noise.Gradient().WithMultipliers(0f, 1f, 0f, 0f));

		[Test]
		public void RoundTrip_CellularDistance() =>
			AssertRoundTripProducesNoise(Noise.CellularDistance());

		[Test]
		public void RoundTrip_CellularLookup() =>
			AssertRoundTripProducesNoise(Noise.CellularLookup(Noise.Simplex()));

		[Test]
		public void RoundTrip_ComplexChain() =>
			AssertRoundTripProducesNoise(
				Noise.Simplex()
					.Fbm(0.5f, 0f, 4, 2f)
					.DomainScale(0.5f)
					.Abs()
					.Terrace(8f, 0.3f)
					.Remap(-1f, 1f, 0f, 1f));

		#endregion

		#region Cross-Encoder Round-Trip

		[Test]
		public void CrossEncoder_CSharpEncodeVsCppDecode_MatchesNoise()
		{
			NoiseNode original = Noise.Simplex().Fbm(0.5f, 0f, 3, 2f);

			using FastNoise directFn = original.Build();
			float[] directData = new float[32 * 32];
			directFn.GenUniformGrid2D(directData, 0f, 0f, 32, 32, 1f, 1f, 1337);

			string encoded = original.Encode();
			using FastNoise cppFn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(cppFn != FastNoise.Invalid, Is.True);
			float[] cppData = new float[32 * 32];
			cppFn.GenUniformGrid2D(cppData, 0f, 0f, 32, 32, 1f, 1f, 1337);

			for (int i = 0; i < directData.Length; i++)
			{
				Assert.That(cppData[i], Is.EqualTo(directData[i]).Within(0.001f),
					$"Mismatch at pixel {i}");
			}
		}

		[Test]
		public void CrossEncoder_CSharpDecodeVsBuild_MatchesNoise()
		{
			NoiseNode original = Noise.Simplex().Fbm(0.5f, 0f, 3, 2f);

			using FastNoise directFn = original.Build();
			float[] directData = new float[32 * 32];
			directFn.GenUniformGrid2D(directData, 0f, 0f, 32, 32, 1f, 1f, 1337);

			string encoded = original.Encode();
			NoiseNode decoded = NoiseNode.Decode(encoded);
			using FastNoise decodedFn = decoded.Build();
			float[] decodedData = new float[32 * 32];
			decodedFn.GenUniformGrid2D(decodedData, 0f, 0f, 32, 32, 1f, 1f, 1337);

			for (int i = 0; i < directData.Length; i++)
			{
				Assert.That(decodedData[i], Is.EqualTo(directData[i]).Within(0.001f),
					$"Mismatch at pixel {i}");
			}
		}

		[Test]
		public void CrossEncoder_FullExampleRoundTrip()
		{
			NoiseNode original = (
				Noise.SuperSimplex().Fbm(0.65f, 0.5f, 4, 2.5f).DomainScale(0.66f)
				+ Noise.Gradient().WithMultipliers(0f, 3f, 0f, 0f)
					.WithOffsets(0f, 0f, 0f, 0f)
			)
				.DomainWarpGradient(0.2f, 2.0f)
				.DomainWarpProgressive(0.7f, 0.5f, 2, 2.5f);

			string encoded = original.Encode();
			Assert.That(encoded, Is.Not.Null.And.Not.Empty);

			using FastNoise cppFn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(cppFn != FastNoise.Invalid, Is.True,
				"C++ failed to decode full example: " + encoded);

			NoiseNode decoded = NoiseNode.Decode(encoded);
			Assert.That(decoded, Is.Not.Null);
			using FastNoise fn = decoded.Build();
			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[64 * 64];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, 64, 64, 1f, 1f, 1337);
			Assert.That(minMax.max, Is.GreaterThan(minMax.min));
		}

		#endregion
	}
}
