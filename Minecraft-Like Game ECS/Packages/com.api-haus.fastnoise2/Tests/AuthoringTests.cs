using NUnit.Framework;
using UnityEngine;

namespace FastNoise2.Tests
{
	using Authoring.NoiseAsset;
	using Authoring.NoiseGraph;
	using Bindings;
	using Generators;

	public class AuthoringTests
	{
		static void AssertProducesNoise(FastNoise fn, int size = 64)
		{
			Assert.That(fn.IsCreated, Is.True);

			float[] data = new float[size * size];
			FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
				data, 0f, 0f, size, size, 0.02f, 0.02f, 1337
			);
			Assert.That(minMax.min, Is.Not.NaN);
			Assert.That(minMax.max, Is.Not.NaN);
			Assert.That(minMax.max, Is.GreaterThanOrEqualTo(minMax.min));
		}

		/// <summary>
		/// Simulates what Unity does when loading a serialized asset:
		/// CreateInstance → JsonUtility round-trip to trigger field initialization.
		/// </summary>
		static T CreateAndDeserialize<T>() where T : ScriptableObject
		{
			var asset = ScriptableObject.CreateInstance<T>();
			string json = JsonUtility.ToJson(asset);
			JsonUtility.FromJsonOverwrite(json, asset);
			return asset;
		}

		#region FastNoiseGraphAsset

		[Test]
		public void CreateGraphAsset_HasNonNullGraph()
		{
			var asset = CreateAndDeserialize<FastNoiseGraphAsset>();
			Assert.That(asset.savedGraph, Is.Not.Null);
			Assert.That(asset.savedGraph.EncodedValue, Is.Not.Null.And.Not.Empty);
			Object.DestroyImmediate(asset);
		}

		[Test]
		public void GraphAsset_DefaultGraph_InstantiatesToValidNoise()
		{
			var asset = CreateAndDeserialize<FastNoiseGraphAsset>();
			using FastNoise fn = asset.savedGraph.Instantiate();
			AssertProducesNoise(fn);
			Object.DestroyImmediate(asset);
		}

		[Test]
		public void GraphAsset_SetEncodedGraph_ProducesNoise()
		{
			var asset = CreateAndDeserialize<FastNoiseGraphAsset>();
			string encoded = Noise.Simplex().Fbm(0.5f, 0f, 3, 2f).Encode();
			asset.savedGraph.SetValue(encoded);

			using FastNoise fn = asset.savedGraph.Instantiate();
			AssertProducesNoise(fn);
			Object.DestroyImmediate(asset);
		}

		#endregion

		#region BakedNoiseTextureAsset

		[Test]
		public void CreateNoiseAsset_HasDefaultValues()
		{
			var asset = CreateAndDeserialize<BakedNoiseTextureAsset>();
			Assert.That(asset.graph, Is.Not.Null);
			Assert.That(asset.graph.EncodedValue, Is.Not.Null.And.Not.Empty);
			Assert.That(asset.frequency, Is.GreaterThan(0f));
			Assert.That(asset.seed, Is.Not.Zero);
			Object.DestroyImmediate(asset);
		}

		[Test]
		public void NoiseAsset_DefaultGraph_InstantiatesToValidNoise()
		{
			var asset = CreateAndDeserialize<BakedNoiseTextureAsset>();
			using FastNoise fn = asset.graph.Instantiate();
			AssertProducesNoise(fn);
			Object.DestroyImmediate(asset);
		}

		[Test]
		public void NoiseAsset_SetEncodedGraph_ProducesNoise()
		{
			var asset = CreateAndDeserialize<BakedNoiseTextureAsset>();
			string encoded = Noise.Perlin().Encode();
			asset.graph.SetValue(encoded);

			using FastNoise fn = asset.graph.Instantiate();
			AssertProducesNoise(fn);
			Object.DestroyImmediate(asset);
		}

		#endregion

		#region FastNoiseGraph

		[Test]
		public void FastNoiseGraph_ImplicitStringConversion()
		{
			string encoded = Noise.Simplex().Encode();
			FastNoiseGraph graph = encoded;
			string back = graph;

			Assert.That(back, Is.EqualTo(encoded));
		}

		[Test]
		public void FastNoiseGraph_Instantiate_ProducesValidNoise()
		{
			string encoded = Noise.Simplex().Encode();
			FastNoiseGraph graph = encoded;

			using FastNoise fn = graph.Instantiate();
			AssertProducesNoise(fn);
		}

		#endregion
	}
}
