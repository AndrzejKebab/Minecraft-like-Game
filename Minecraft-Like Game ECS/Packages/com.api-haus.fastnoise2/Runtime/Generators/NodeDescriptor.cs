using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Immutable data object describing a noise node and its parameters.
	/// Serves as the intermediate representation (IR) for the noise graph,
	/// enabling both native handle construction and binary serialization.
	/// </summary>
	public sealed class NodeDescriptor
	{
		public string NodeName { get; }
		public IReadOnlyDictionary<string, int> Variables { get; }
		public IReadOnlyDictionary<string, NodeDescriptor> NodeLookups { get; }
		public IReadOnlyDictionary<string, HybridValue> Hybrids { get; }

		static readonly IReadOnlyDictionary<string, int> s_EmptyVars =
			new Dictionary<string, int>();

		static readonly IReadOnlyDictionary<string, NodeDescriptor> s_EmptyNodes =
			new Dictionary<string, NodeDescriptor>();

		static readonly IReadOnlyDictionary<string, HybridValue> s_EmptyHybrids =
			new Dictionary<string, HybridValue>();

		public NodeDescriptor(string nodeName,
			IReadOnlyDictionary<string, int> variables = null,
			IReadOnlyDictionary<string, NodeDescriptor> nodeLookups = null,
			IReadOnlyDictionary<string, HybridValue> hybrids = null)
		{
			NodeName = nodeName;
			Variables = variables ?? s_EmptyVars;
			NodeLookups = nodeLookups ?? s_EmptyNodes;
			Hybrids = hybrids ?? s_EmptyHybrids;
		}
	}

	/// <summary>
	/// A hybrid value that is either a float constant or a child node descriptor.
	/// </summary>
	public readonly struct HybridValue
	{
		public bool IsNode { get; }
		public float FloatValue { get; }
		public NodeDescriptor NodeValue { get; }

		public HybridValue(float value)
		{
			IsNode = false;
			FloatValue = value;
			NodeValue = null;
		}

		public HybridValue(NodeDescriptor node)
		{
			IsNode = true;
			FloatValue = 0f;
			NodeValue = node;
		}
	}
}
