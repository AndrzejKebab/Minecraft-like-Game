using NUnit.Framework;

namespace FastNoise2.Tests
{
	using Bindings;
	using Generators;

	public class GeneratorTests
	{
		static void AssertProducesNoise(NoiseNode node, int size = 64)
		{
			using FastNoise fn = node.Build();
			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[size * size];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, size, size, 0.02f, 0.02f, 1337
			);
			Assert.That(minMax.min, Is.Not.NaN);
			Assert.That(minMax.max, Is.Not.NaN);
			Assert.That(minMax.max, Is.GreaterThanOrEqualTo(minMax.min));
		}

		#region Factory Methods

		[Test]
		public void Constant()
		{
			using FastNoise fn = Noise.Constant(0.5f).Build();
			Assert.That(fn.IsCreated, Is.True);
			Assert.That(fn.GenSingle2D(0f, 0f, 1337), Is.EqualTo(0.5f).Within(0.001f));
		}

		[Test]
		public void White() => AssertProducesNoise(Noise.White());

		[Test]
		public void Checkerboard() => AssertProducesNoise(Noise.Checkerboard());

		[Test]
		public void SineWave() => AssertProducesNoise(Noise.SineWave());

		[Test]
		public void Simplex() => AssertProducesNoise(Noise.Simplex());

		[Test]
		public void SuperSimplex() => AssertProducesNoise(Noise.SuperSimplex());

		[Test]
		public void Perlin() => AssertProducesNoise(Noise.Perlin());

		[Test]
		public void Value() => AssertProducesNoise(Noise.Value());

		[Test]
		public void Gradient() => AssertProducesNoise(Noise.Gradient());

		[Test]
		public void DistanceToPoint() => AssertProducesNoise(Noise.DistanceToPoint());

		[Test]
		public void CellularValue() => AssertProducesNoise(Noise.CellularValue());

		[Test]
		public void CellularDistance() => AssertProducesNoise(Noise.CellularDistance());

		[Test]
		public void CellularLookup() =>
			AssertProducesNoise(Noise.CellularLookup(Noise.Simplex()));

		#endregion

		#region Operators

		[Test]
		public void AddNodes()
		{
			NoiseNode sum = Noise.Simplex() + Noise.Perlin();
			AssertProducesNoise(sum);
		}

		[Test]
		public void AddFloat()
		{
			NoiseNode shifted = Noise.Simplex() + 1f;
			AssertProducesNoise(shifted);
		}

		[Test]
		public void FloatAddNode()
		{
			NoiseNode shifted = 1f + Noise.Simplex();
			AssertProducesNoise(shifted);
		}

		[Test]
		public void SubtractNodes()
		{
			NoiseNode diff = Noise.Simplex() - Noise.Perlin();
			AssertProducesNoise(diff);
		}

		[Test]
		public void SubtractFloat()
		{
			NoiseNode shifted = Noise.Simplex() - 0.5f;
			AssertProducesNoise(shifted);
		}

		[Test]
		public void MultiplyNodes()
		{
			NoiseNode product = Noise.Simplex() * Noise.Perlin();
			AssertProducesNoise(product);
		}

		[Test]
		public void MultiplyFloat()
		{
			NoiseNode scaled = Noise.Simplex() * 2f;
			AssertProducesNoise(scaled);
		}

		[Test]
		public void DivideNodes()
		{
			NoiseNode quotient = Noise.Simplex() / Noise.Constant(2f);
			AssertProducesNoise(quotient);
		}

		[Test]
		public void DivideFloat()
		{
			NoiseNode quotient = Noise.Simplex() / 2f;
			AssertProducesNoise(quotient);
		}

		[Test]
		public void ModulusNodes()
		{
			NoiseNode mod = Noise.Simplex() % Noise.Constant(0.5f);
			AssertProducesNoise(mod);
		}

		[Test]
		public void ModulusFloat()
		{
			NoiseNode mod = Noise.Simplex() % 0.5f;
			AssertProducesNoise(mod);
		}

		[Test]
		public void Negate()
		{
			NoiseNode neg = -Noise.Simplex();
			using FastNoise fn = neg.Build();
			Assert.That(fn.IsCreated, Is.True);

			using FastNoise original = Noise.Simplex().Build();
			float v1 = fn.GenSingle2D(1f, 1f, 1337);
			float v2 = original.GenSingle2D(1f, 1f, 1337);
			Assert.That(v1, Is.EqualTo(-v2).Within(0.001f));
		}

		#endregion

		#region Fractal

		[Test]
		public void Fbm()
		{
			NoiseNode fbm = Noise.Simplex().Fbm(0.5f, 0f, 5, 2f);
			AssertProducesNoise(fbm);
		}

		[Test]
		public void FbmHybridGain()
		{
			NoiseNode fbm = Noise.Simplex().Fbm(Noise.Perlin(), 0f, 3, 2f);
			AssertProducesNoise(fbm);
		}

		[Test]
		public void Ridged()
		{
			NoiseNode ridged = Noise.Simplex().Ridged(0.5f, 0f, 4, 2f);
			AssertProducesNoise(ridged);
		}

		#endregion

		#region Domain Warp

		[Test]
		public void DomainWarpGradient()
		{
			DomainWarpNode warp = Noise.Simplex().DomainWarpGradient(0.5f, 2f);
			AssertProducesNoise(warp);
		}

		[Test]
		public void DomainWarpProgressive()
		{
			NoiseNode node = Noise.Simplex()
				.DomainWarpGradient(0.5f, 2f)
				.DomainWarpProgressive(0.5f, 0f, 3, 2f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DomainWarpIndependent()
		{
			NoiseNode node = Noise.Simplex()
				.DomainWarpGradient(0.5f, 2f)
				.DomainWarpIndependent(0.5f, 0f, 3, 2f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DomainWarpSimplex()
		{
			DomainWarpNode warp = Noise.Simplex().DomainWarpSimplex(0.5f, 2f);
			AssertProducesNoise(warp);
		}

		[Test]
		public void DomainWarpSuperSimplex()
		{
			DomainWarpNode warp = Noise.Simplex().DomainWarpSuperSimplex(0.5f, 2f);
			AssertProducesNoise(warp);
		}

		#endregion

		#region Modifiers

		[Test]
		public void DomainScale()
		{
			NoiseNode node = Noise.Simplex().DomainScale(0.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DomainOffset()
		{
			NoiseNode node = Noise.Simplex().DomainOffset(1f, 2f, 3f, 0f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DomainRotate()
		{
			NoiseNode node = Noise.Simplex().DomainRotate(45f, 0f, 0f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DomainAxisScale()
		{
			NoiseNode node = Noise.Simplex().DomainAxisScale(2f, 1f, 1f, 1f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DomainRotatePlane()
		{
			NoiseNode node = Noise.Simplex().DomainRotatePlane(PlaneRotationType.ImproveXZPlanes);
			AssertProducesNoise(node);
		}

		[Test]
		public void SeedOffset()
		{
			NoiseNode node = Noise.Simplex().SeedOffset(42);
			AssertProducesNoise(node);
		}

		[Test]
		public void Remap()
		{
			NoiseNode node = Noise.Simplex().Remap(-1f, 1f, 0f, 1f);
			AssertProducesNoise(node);
		}

		[Test]
		public void Terrace()
		{
			NoiseNode node = Noise.Simplex().Terrace(4f, 0.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void TerraceHybridSmoothness()
		{
			NoiseNode node = Noise.Simplex().Terrace(4f, Noise.Perlin());
			AssertProducesNoise(node);
		}

		[Test]
		public void AddDimension()
		{
			NoiseNode node = Noise.Simplex().AddDimension(0.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void RemoveDimension()
		{
			NoiseNode node = Noise.Simplex().RemoveDimension(Dimension.W);
			AssertProducesNoise(node);
		}

		[Test]
		public void Cache()
		{
			NoiseNode node = Noise.Simplex().Cache();
			AssertProducesNoise(node);
		}

		[Test]
		public void PingPong()
		{
			NoiseNode node = Noise.Simplex().PingPong(1.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void Abs()
		{
			NoiseNode node = Noise.Simplex().Abs();
			AssertProducesNoise(node);
		}

		[Test]
		public void SignedSqrt()
		{
			NoiseNode node = Noise.Simplex().SignedSqrt();
			AssertProducesNoise(node);
		}

		#endregion

		#region Blend

		[Test]
		public void MinBlend()
		{
			NoiseNode node = Noise.Simplex().Min(Noise.Perlin());
			AssertProducesNoise(node);
		}

		[Test]
		public void MaxBlend()
		{
			NoiseNode node = Noise.Simplex().Max(Noise.Perlin());
			AssertProducesNoise(node);
		}

		[Test]
		public void MinSmooth()
		{
			NoiseNode node = Noise.Simplex().MinSmooth(Noise.Perlin(), 0.2f);
			AssertProducesNoise(node);
		}

		[Test]
		public void MaxSmooth()
		{
			NoiseNode node = Noise.Simplex().MaxSmooth(Noise.Perlin(), 0.2f);
			AssertProducesNoise(node);
		}

		[Test]
		public void PowFloat()
		{
			NoiseNode node = Noise.Simplex().Abs().PowFloat(2f);
			AssertProducesNoise(node);
		}

		[Test]
		public void PowInt()
		{
			NoiseNode node = Noise.Simplex().PowInt(2);
			AssertProducesNoise(node);
		}

		#endregion

		#region Node Subclasses

		[Test]
		public void GradientWithMultipliers()
		{
			GradientNode node = Noise.Gradient()
				.WithMultipliers(0f, 3f, 0f, 0f)
				.WithOffsets(0f, 0f, 0f, 0f);
			AssertProducesNoise(node);
		}

		[Test]
		public void GradientPerAxisMethods()
		{
			GradientNode node = Noise.Gradient()
				.WithMultiplierX(0f)
				.WithMultiplierY(1f)
				.WithOffsetX(0.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void DistanceToPointWithConfig()
		{
			DistanceToPointNode node = Noise.DistanceToPoint()
				.WithDistanceFunction(DistanceFunction.Manhattan)
				.WithPoint(0.5f, 0.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void CellularDistanceWithConfig()
		{
			CellularDistanceNode node = Noise.CellularDistance()
				.WithDistanceFunction(DistanceFunction.Manhattan)
				.WithReturnType(CellularReturnType.Index0Add1)
				.WithDistanceIndex0(0)
				.WithDistanceIndex1(2)
				.WithGridJitter(0.5f);
			AssertProducesNoise(node);
		}

		[Test]
		public void CellularDistanceHybridJitter()
		{
			CellularDistanceNode node = Noise.CellularDistance()
				.WithGridJitter(Noise.Simplex());
			AssertProducesNoise(node);
		}

		[Test]
		public void CellularValueWithConfig()
		{
			CellularValueNode node = Noise.CellularValue()
				.WithDistanceFunction(DistanceFunction.EuclideanSquared)
				.WithValueIndex(1)
				.WithGridJitter(0.8f);
			AssertProducesNoise(node);
		}

		[Test]
		public void CellularLookupWithConfig()
		{
			CellularLookupNode node = Noise.CellularLookup(Noise.Simplex())
				.WithDistanceFunction(DistanceFunction.Manhattan)
				.WithGridJitter(0.5f);
			AssertProducesNoise(node);
		}

		#endregion

		#region Integration

		[Test]
		public void FullExampleFromRequirements()
		{
			using FastNoise fn = (
				Noise.SuperSimplex().Fbm(0.65f, 0.5f, 4, 2.5f).DomainScale(0.66f)
				+ Noise.Gradient().WithMultipliers(0f, 3f, 0f, 0f).WithOffsets(0f, 0f, 0f, 0f)
			)
				.DomainWarpGradient(0.2f, 2.0f)
				.DomainWarpProgressive(0.7f, 0.5f, 2, 2.5f)
				.Build();

			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[128 * 128];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, 128, 128, 0.02f, 0.02f, 1337
			);
			Assert.That(minMax.max, Is.GreaterThan(minMax.min));
		}

		[Test]
		public void ComplexChain()
		{
			NoiseNode node = Noise.Simplex()
				.Fbm(0.5f, 0f, 4, 2f)
				.DomainScale(0.5f)
				.Abs()
				.Terrace(8f, 0.3f)
				.Remap(-1f, 1f, 0f, 1f);
			AssertProducesNoise(node);
		}

		[Test]
		public void MultipleBuildCallsProduceIndependentHandles()
		{
			NoiseNode node = Noise.Simplex();

			using FastNoise a = node.Build();
			using FastNoise b = node.Build();

			Assert.That(a.IsCreated, Is.True);
			Assert.That(b.IsCreated, Is.True);
			Assert.That(a, Is.Not.EqualTo(b));
		}

		#endregion
	}
}
