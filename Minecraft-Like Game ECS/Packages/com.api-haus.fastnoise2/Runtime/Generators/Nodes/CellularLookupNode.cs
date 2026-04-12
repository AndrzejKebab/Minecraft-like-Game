using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Builder for CellularLookup noise. Immutable: each <c>With*</c> returns a new instance.
	/// </summary>
	public class CellularLookupNode : NoiseNode
	{
		readonly NoiseNode m_Lookup;
		readonly DistanceFunction m_DistFunc;
		readonly Hybrid m_GridJitter, m_SizeJitter, m_MinkowskiP;

		internal CellularLookupNode(NoiseNode lookup, DistanceFunction distFunc,
			Hybrid gridJitter, Hybrid sizeJitter, Hybrid minkowskiP)
			: base(MakeDescriptor(lookup, distFunc, gridJitter, sizeJitter, minkowskiP))
		{
			m_Lookup = lookup;
			m_DistFunc = distFunc;
			m_GridJitter = gridJitter;
			m_SizeJitter = sizeJitter;
			m_MinkowskiP = minkowskiP;
		}

		static NodeDescriptor MakeDescriptor(NoiseNode lookup, DistanceFunction distFunc,
			Hybrid gridJitter, Hybrid sizeJitter, Hybrid minkowskiP)
		{
			var vars = new Dictionary<string, int>
			{
				{ "DistanceFunction", EnumIndex("CellularLookup",
					"DistanceFunction", distFunc.ToMetadataString()) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Lookup", lookup.m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			gridJitter.AddTo(hybrids, "GridJitter");
			sizeJitter.AddTo(hybrids, "SizeJitter");
			minkowskiP.AddTo(hybrids, "MinkowskiP");
			return new NodeDescriptor("CellularLookup", vars, nodes, hybrids);
		}

		public CellularLookupNode WithDistanceFunction(DistanceFunction value) =>
			new(m_Lookup, value, m_GridJitter, m_SizeJitter, m_MinkowskiP);

		public CellularLookupNode WithGridJitter(Hybrid value) =>
			new(m_Lookup, m_DistFunc, value, m_SizeJitter, m_MinkowskiP);

		public CellularLookupNode WithSizeJitter(Hybrid value) =>
			new(m_Lookup, m_DistFunc, m_GridJitter, value, m_MinkowskiP);

		public CellularLookupNode WithMinkowskiP(Hybrid value) =>
			new(m_Lookup, m_DistFunc, m_GridJitter, m_SizeJitter, value);
	}
}
