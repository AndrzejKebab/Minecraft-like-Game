using System.Collections.Generic;
using FastNoise2.Bindings;

namespace FastNoise2.Generators
{
	using System;

	/// <summary>
	/// Represents a FN2 hybrid parameter that accepts either a float value or a generator node.
	/// </summary>
	public readonly struct Hybrid
	{
		readonly float m_Float;
		readonly NoiseNode m_Node;
		readonly bool m_IsNode;

		Hybrid(float value)
		{
			m_Float = value;
			m_Node = null;
			m_IsNode = false;
		}

		Hybrid(NoiseNode node)
		{
			m_Float = 0f;
			m_Node = node ?? throw new ArgumentNullException(nameof(node));
			m_IsNode = true;
		}

		public static implicit operator Hybrid(float value) => new(value);

		public static implicit operator Hybrid(NoiseNode node) => new(node);

		internal void Apply(FastNoise target, string memberName)
		{
			if (m_IsNode)
				target.Set(memberName, m_Node.Build());
			else
				target.Set(memberName, m_Float);
		}

		internal HybridValue ToHybridValue() =>
			m_IsNode ? new HybridValue(m_Node.m_Descriptor) : new HybridValue(m_Float);

		internal void AddTo(Dictionary<string, HybridValue> dict, string name) =>
			dict[name] = ToHybridValue();
	}
}
