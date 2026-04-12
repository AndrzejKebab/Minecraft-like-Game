using System;
using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Builder for Gradient (position output) noise with per-axis multipliers and offsets.
	/// Immutable: each <c>With*</c> method returns a new instance.
	/// </summary>
	public class GradientNode : NoiseNode
	{
		readonly float m_Mx, m_My, m_Mz, m_Mw;
		readonly float m_Ox, m_Oy, m_Oz, m_Ow;

		static int B(float v) => BitConverter.SingleToInt32Bits(v);

		internal GradientNode(float mx, float my, float mz, float mw,
			float ox, float oy, float oz, float ow)
			: base(MakeDescriptor(mx, my, mz, mw, ox, oy, oz, ow))
		{
			m_Mx = mx;
			m_My = my;
			m_Mz = mz;
			m_Mw = mw;
			m_Ox = ox;
			m_Oy = oy;
			m_Oz = oz;
			m_Ow = ow;
		}

		static NodeDescriptor MakeDescriptor(float mx, float my, float mz, float mw,
			float ox, float oy, float oz, float ow) =>
			new("Gradient",
				variables: new Dictionary<string, int>
				{
					{ "MultiplierX", B(mx) },
					{ "MultiplierY", B(my) },
					{ "MultiplierZ", B(mz) },
					{ "MultiplierW", B(mw) }
				},
				hybrids: new Dictionary<string, HybridValue>
				{
					{ "OffsetX", new HybridValue(ox) },
					{ "OffsetY", new HybridValue(oy) },
					{ "OffsetZ", new HybridValue(oz) },
					{ "OffsetW", new HybridValue(ow) }
				});

		public GradientNode WithMultipliers(float x, float y, float z = 0f, float w = 0f) =>
			new(x, y, z, w, m_Ox, m_Oy, m_Oz, m_Ow);

		public GradientNode WithMultiplierX(float value) =>
			new(value, m_My, m_Mz, m_Mw, m_Ox, m_Oy, m_Oz, m_Ow);

		public GradientNode WithMultiplierY(float value) =>
			new(m_Mx, value, m_Mz, m_Mw, m_Ox, m_Oy, m_Oz, m_Ow);

		public GradientNode WithMultiplierZ(float value) =>
			new(m_Mx, m_My, value, m_Mw, m_Ox, m_Oy, m_Oz, m_Ow);

		public GradientNode WithMultiplierW(float value) =>
			new(m_Mx, m_My, m_Mz, value, m_Ox, m_Oy, m_Oz, m_Ow);

		public GradientNode WithOffsets(float x, float y, float z = 0f, float w = 0f) =>
			new(m_Mx, m_My, m_Mz, m_Mw, x, y, z, w);

		public GradientNode WithOffsetX(float value) =>
			new(m_Mx, m_My, m_Mz, m_Mw, value, m_Oy, m_Oz, m_Ow);

		public GradientNode WithOffsetY(float value) =>
			new(m_Mx, m_My, m_Mz, m_Mw, m_Ox, value, m_Oz, m_Ow);

		public GradientNode WithOffsetZ(float value) =>
			new(m_Mx, m_My, m_Mz, m_Mw, m_Ox, m_Oy, value, m_Ow);

		public GradientNode WithOffsetW(float value) =>
			new(m_Mx, m_My, m_Mz, m_Mw, m_Ox, m_Oy, m_Oz, value);
	}
}
