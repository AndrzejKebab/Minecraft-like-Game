using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Builder for CellularDistance noise. Immutable: each <c>With*</c> returns a new instance.
	/// </summary>
	public class CellularDistanceNode : NoiseNode
	{
		readonly DistanceFunction m_DistFunc;
		readonly CellularReturnType m_ReturnType;
		readonly int m_DistIdx0, m_DistIdx1;
		readonly Hybrid m_GridJitter, m_SizeJitter, m_MinkowskiP;

		internal CellularDistanceNode(DistanceFunction distFunc,
			CellularReturnType returnType, int distIdx0, int distIdx1,
			Hybrid gridJitter, Hybrid sizeJitter, Hybrid minkowskiP)
			: base(MakeDescriptor(distFunc, returnType, distIdx0, distIdx1,
				gridJitter, sizeJitter, minkowskiP))
		{
			m_DistFunc = distFunc;
			m_ReturnType = returnType;
			m_DistIdx0 = distIdx0;
			m_DistIdx1 = distIdx1;
			m_GridJitter = gridJitter;
			m_SizeJitter = sizeJitter;
			m_MinkowskiP = minkowskiP;
		}

		static NodeDescriptor MakeDescriptor(DistanceFunction distFunc,
			CellularReturnType returnType, int distIdx0, int distIdx1,
			Hybrid gridJitter, Hybrid sizeJitter, Hybrid minkowskiP)
		{
			var vars = new Dictionary<string, int>
			{
				{ "DistanceFunction", EnumIndex("CellularDistance",
					"DistanceFunction", distFunc.ToMetadataString()) },
				{ "ReturnType", EnumIndex("CellularDistance",
					"ReturnType", returnType.ToMetadataString()) },
				{ "DistanceIndex0", distIdx0 },
				{ "DistanceIndex1", distIdx1 }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			gridJitter.AddTo(hybrids, "GridJitter");
			sizeJitter.AddTo(hybrids, "SizeJitter");
			minkowskiP.AddTo(hybrids, "MinkowskiP");
			return new NodeDescriptor("CellularDistance", vars, hybrids: hybrids);
		}

		public CellularDistanceNode WithDistanceFunction(DistanceFunction value) =>
			new(value, m_ReturnType, m_DistIdx0, m_DistIdx1,
				m_GridJitter, m_SizeJitter, m_MinkowskiP);

		public CellularDistanceNode WithReturnType(CellularReturnType value) =>
			new(m_DistFunc, value, m_DistIdx0, m_DistIdx1,
				m_GridJitter, m_SizeJitter, m_MinkowskiP);

		public CellularDistanceNode WithDistanceIndex0(int value) =>
			new(m_DistFunc, m_ReturnType, value, m_DistIdx1,
				m_GridJitter, m_SizeJitter, m_MinkowskiP);

		public CellularDistanceNode WithDistanceIndex1(int value) =>
			new(m_DistFunc, m_ReturnType, m_DistIdx0, value,
				m_GridJitter, m_SizeJitter, m_MinkowskiP);

		public CellularDistanceNode WithGridJitter(Hybrid value) =>
			new(m_DistFunc, m_ReturnType, m_DistIdx0, m_DistIdx1,
				value, m_SizeJitter, m_MinkowskiP);

		public CellularDistanceNode WithSizeJitter(Hybrid value) =>
			new(m_DistFunc, m_ReturnType, m_DistIdx0, m_DistIdx1,
				m_GridJitter, value, m_MinkowskiP);

		public CellularDistanceNode WithMinkowskiP(Hybrid value) =>
			new(m_DistFunc, m_ReturnType, m_DistIdx0, m_DistIdx1,
				m_GridJitter, m_SizeJitter, value);
	}
}
