using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// A noise node produced by domain warp methods. Only domain warp nodes
	/// can be chained with fractal domain warp operations.
	/// </summary>
	public class DomainWarpNode : NoiseNode
	{
		internal DomainWarpNode(NodeDescriptor descriptor) : base(descriptor)
		{
		}

		public NoiseNode DomainWarpProgressive(Hybrid gain = default,
			Hybrid weightedStrength = default, int octaves = 3, float lacunarity = 2f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Octaves", octaves },
				{ "Lacunarity", Bits(lacunarity) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "DomainWarpSource", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			gain.AddTo(hybrids, "Gain");
			weightedStrength.AddTo(hybrids, "WeightedStrength");
			return new NoiseNode(new NodeDescriptor("DomainWarpFractalProgressive",
				vars, nodes, hybrids));
		}

		public NoiseNode DomainWarpIndependent(Hybrid gain = default,
			Hybrid weightedStrength = default, int octaves = 3, float lacunarity = 2f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Octaves", octaves },
				{ "Lacunarity", Bits(lacunarity) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "DomainWarpSource", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			gain.AddTo(hybrids, "Gain");
			weightedStrength.AddTo(hybrids, "WeightedStrength");
			return new NoiseNode(new NodeDescriptor("DomainWarpFractalIndependent",
				vars, nodes, hybrids));
		}
	}
}
