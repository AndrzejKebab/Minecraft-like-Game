using System;
using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Static factory for creating noise generator nodes.
	/// </summary>
	public static class Noise
	{
		static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

		#region Basic

		public static NoiseNode Constant(float value = 1f) =>
			new(new NodeDescriptor("Constant",
				new Dictionary<string, int> { { "Value", Bits(value) } }));

		public static NoiseNode White(int seedOffset = 0)
		{
			var vars = new Dictionary<string, int>();
			if (seedOffset != 0)
				vars["SeedOffset"] = seedOffset;
			return new NoiseNode(new NodeDescriptor("White", vars));
		}

		public static NoiseNode Checkerboard(float featureScale = 100f) =>
			new(new NodeDescriptor("Checkerboard",
				new Dictionary<string, int> { { "FeatureScale", Bits(featureScale) } }));

		public static NoiseNode SineWave(float featureScale = 100f) =>
			new(new NodeDescriptor("SineWave",
				new Dictionary<string, int> { { "FeatureScale", Bits(featureScale) } }));

		public static GradientNode Gradient() => new(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

		public static DistanceToPointNode DistanceToPoint() =>
			new(DistanceFunction.Euclidean, 0f, 0f, 0f, 0f, 0f);

		#endregion

		#region Coherent Noise

		static NoiseNode CoherentNoise(string name, float featureScale, int seedOffset)
		{
			var vars = new Dictionary<string, int>
			{
				{ "FeatureScale", Bits(featureScale) }
			};
			if (seedOffset != 0)
				vars["SeedOffset"] = seedOffset;
			return new NoiseNode(new NodeDescriptor(name, vars));
		}

		public static NoiseNode Simplex(float featureScale = 100f, int seedOffset = 0) =>
			CoherentNoise("Simplex", featureScale, seedOffset);

		public static NoiseNode SuperSimplex(float featureScale = 100f, int seedOffset = 0) =>
			CoherentNoise("SuperSimplex", featureScale, seedOffset);

		public static NoiseNode Perlin(float featureScale = 100f, int seedOffset = 0) =>
			CoherentNoise("Perlin", featureScale, seedOffset);

		public static NoiseNode Value(float featureScale = 100f, int seedOffset = 0) =>
			CoherentNoise("Value", featureScale, seedOffset);

		#endregion

		#region Cellular

		public static CellularValueNode CellularValue() =>
			new(DistanceFunction.Euclidean, 0, 0f, 0f, 0f);

		public static CellularDistanceNode CellularDistance() =>
			new(DistanceFunction.Euclidean, CellularReturnType.Index0, 0, 1, 0f, 0f, 0f);

		public static CellularLookupNode CellularLookup(NoiseNode lookup) =>
			new(lookup, DistanceFunction.Euclidean, 0f, 0f, 0f);

		#endregion
	}
}
