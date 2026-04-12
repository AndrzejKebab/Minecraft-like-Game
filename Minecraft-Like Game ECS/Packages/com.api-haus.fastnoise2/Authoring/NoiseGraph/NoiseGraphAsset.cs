using FastNoise2.Bindings;
using FastNoise2.Generators;
using UnityEngine;

namespace FastNoise2.Authoring.NoiseGraph
{
	/// <summary>
	/// Runtime-accessible ScriptableObject that holds a compiled noise graph
	/// and provides lazy construction of native handles.
	/// </summary>
	[Icon("Packages/com.api-haus.fastnoise2/Editor/Resources/Icons/NoiseGraph.png")]
	public class NoiseGraphAsset : ScriptableObject
	{
		[SerializeField] string encodedGraph;

		public bool IsValid => !string.IsNullOrEmpty(encodedGraph);

		/// <summary>
		/// Returns a deferred <see cref="NoiseNode"/> that can be further composed
		/// with the fluent builder API before materializing.
		/// </summary>
		public NoiseNode ToNoiseNode() =>
			IsValid ? NoiseNode.Decode(encodedGraph) : null;

		/// <summary>
		/// Materializes the noise graph into a native <see cref="FastNoise"/> handle.
		/// The caller owns the returned handle and must dispose it.
		/// </summary>
		public FastNoise CreateNoise() =>
			IsValid ? FastNoise.FromEncodedNodeTree(encodedGraph) : default;

		internal void SetEncodedGraph(string value) => encodedGraph = value;
	}
}
