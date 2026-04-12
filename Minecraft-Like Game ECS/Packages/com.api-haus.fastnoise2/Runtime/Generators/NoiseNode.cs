using System;
using System.Collections.Generic;
using FastNoise2.Bindings;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Deferred noise graph node. Stores a <see cref="NodeDescriptor"/> that describes
	/// the noise node and its parameters. Fluent methods return new nodes that reference
	/// the parent descriptor, forming an immutable computation graph.
	/// </summary>
	public class NoiseNode
	{
		internal readonly NodeDescriptor m_Descriptor;

		internal NoiseNode(NodeDescriptor descriptor)
		{
			m_Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
		}

		/// <summary>
		/// The underlying descriptor tree for this noise node.
		/// </summary>
		public NodeDescriptor Descriptor => m_Descriptor;

		/// <summary>
		/// Materializes the noise graph into a native <see cref="FastNoise"/> handle.
		/// The caller owns the returned handle and must dispose it.
		/// </summary>
		public FastNoise Build() => NodeBuilder.Build(m_Descriptor);

		/// <summary>
		/// Encodes the noise graph into FN2's base64 binary format,
		/// interoperable with the C++ NoiseTool.
		/// </summary>
		public string Encode() => NodeEncoder.Encode(m_Descriptor);

		/// <summary>
		/// Decodes an FN2 base64 encoded node tree string into a <see cref="NoiseNode"/>.
		/// Returns a generic NoiseNode (not typed subclasses) since the base64 format
		/// has no concept of the C# class hierarchy.
		/// </summary>
		public static NoiseNode Decode(string encodedNodeTree) =>
			new(NodeDecoder.Decode(encodedNodeTree));

		internal static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

		internal static int EnumIndex(string nodeName, string memberName, string enumValue)
		{
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef(nodeName);
			string key = memberName.Replace(" ", "").ToLower();
			if (!def.TryGetMember(key, out var member))
				throw new ArgumentException($"Unknown member '{memberName}' on '{nodeName}'");
			if (member.EnumValues == null)
				throw new ArgumentException($"Member '{memberName}' on '{nodeName}' is not an enum");
			string enumKey = enumValue.Replace(" ", "").ToLower();
			for (int i = 0; i < member.EnumValues.Length; i++)
			{
				if (member.EnumValues[i].Replace(" ", "").Equals(enumKey,
						StringComparison.OrdinalIgnoreCase))
					return i;
			}
			throw new ArgumentException($"Unknown enum value '{enumValue}' for '{memberName}'");
		}

		#region Fractal

		public NoiseNode Fbm(Hybrid gain = default, Hybrid weightedStrength = default,
			int octaves = 3, float lacunarity = 2f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Octaves", octaves },
				{ "Lacunarity", Bits(lacunarity) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			gain.AddTo(hybrids, "Gain");
			weightedStrength.AddTo(hybrids, "WeightedStrength");
			return new NoiseNode(new NodeDescriptor("FractalFBm", vars, nodes, hybrids));
		}

		public NoiseNode Ridged(Hybrid gain = default, Hybrid weightedStrength = default,
			int octaves = 3, float lacunarity = 2f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Octaves", octaves },
				{ "Lacunarity", Bits(lacunarity) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			gain.AddTo(hybrids, "Gain");
			weightedStrength.AddTo(hybrids, "WeightedStrength");
			return new NoiseNode(new NodeDescriptor("FractalRidged", vars, nodes, hybrids));
		}

		#endregion

		#region Blend

		public NoiseNode Min(Hybrid rhs)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "LHS", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			rhs.AddTo(hybrids, "RHS");
			return new NoiseNode(new NodeDescriptor("Min", nodeLookups: nodes, hybrids: hybrids));
		}

		public NoiseNode Max(Hybrid rhs)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "LHS", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			rhs.AddTo(hybrids, "RHS");
			return new NoiseNode(new NodeDescriptor("Max", nodeLookups: nodes, hybrids: hybrids));
		}

		public NoiseNode MinSmooth(Hybrid rhs, Hybrid smoothness = default)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "LHS", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			rhs.AddTo(hybrids, "RHS");
			smoothness.AddTo(hybrids, "Smoothness");
			return new NoiseNode(new NodeDescriptor("MinSmooth", nodeLookups: nodes, hybrids: hybrids));
		}

		public NoiseNode MaxSmooth(Hybrid rhs, Hybrid smoothness = default)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "LHS", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			rhs.AddTo(hybrids, "RHS");
			smoothness.AddTo(hybrids, "Smoothness");
			return new NoiseNode(new NodeDescriptor("MaxSmooth", nodeLookups: nodes, hybrids: hybrids));
		}

		public NoiseNode Fade(NoiseNode b, Hybrid fade = default,
			Hybrid fadeMin = default, Hybrid fadeMax = default,
			FadeInterpolation interpolation = FadeInterpolation.Linear)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Interpolation", EnumIndex("Fade", "Interpolation",
					interpolation.ToMetadataString()) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "A", m_Descriptor },
				{ "B", b.m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			fade.AddTo(hybrids, "Fade");
			fadeMin.AddTo(hybrids, "FadeMin");
			fadeMax.AddTo(hybrids, "FadeMax");
			return new NoiseNode(new NodeDescriptor("Fade", vars, nodes, hybrids));
		}

		public NoiseNode PowFloat(Hybrid pow)
		{
			var hybrids = new Dictionary<string, HybridValue>();
			Hybrid sourceHybrid = this;
			sourceHybrid.AddTo(hybrids, "Value");
			pow.AddTo(hybrids, "Pow");
			return new NoiseNode(new NodeDescriptor("PowFloat", hybrids: hybrids));
		}

		public NoiseNode PowInt(int pow = 2)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Pow", pow }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Value", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("PowInt", vars, nodes));
		}

		#endregion

		#region Domain Warp

		public DomainWarpNode DomainWarpGradient(Hybrid warpAmplitude = default,
			float featureScale = 100f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "FeatureScale", Bits(featureScale) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			warpAmplitude.AddTo(hybrids, "WarpAmplitude");
			return new DomainWarpNode(
				new NodeDescriptor("DomainWarpGradient", vars, nodes, hybrids));
		}

		public DomainWarpNode DomainWarpSimplex(Hybrid warpAmplitude = default,
			float featureScale = 100f,
			VectorizationScheme scheme = VectorizationScheme.OrthogonalGradientMatrix)
		{
			var vars = new Dictionary<string, int>
			{
				{ "FeatureScale", Bits(featureScale) },
				{ "VectorizationScheme", EnumIndex("DomainWarpSimplex",
					"VectorizationScheme", scheme.ToMetadataString()) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			warpAmplitude.AddTo(hybrids, "WarpAmplitude");
			return new DomainWarpNode(
				new NodeDescriptor("DomainWarpSimplex", vars, nodes, hybrids));
		}

		public DomainWarpNode DomainWarpSuperSimplex(Hybrid warpAmplitude = default,
			float featureScale = 100f,
			VectorizationScheme scheme = VectorizationScheme.OrthogonalGradientMatrix)
		{
			var vars = new Dictionary<string, int>
			{
				{ "FeatureScale", Bits(featureScale) },
				{ "VectorizationScheme", EnumIndex("DomainWarpSuperSimplex",
					"VectorizationScheme", scheme.ToMetadataString()) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			warpAmplitude.AddTo(hybrids, "WarpAmplitude");
			return new DomainWarpNode(
				new NodeDescriptor("DomainWarpSuperSimplex", vars, nodes, hybrids));
		}

		#endregion

		#region Modifiers

		public NoiseNode DomainScale(float scaling = 1f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Scaling", Bits(scaling) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("DomainScale", vars, nodes));
		}

		public NoiseNode DomainOffset(Hybrid x = default, Hybrid y = default,
			Hybrid z = default, Hybrid w = default)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			x.AddTo(hybrids, "OffsetX");
			y.AddTo(hybrids, "OffsetY");
			z.AddTo(hybrids, "OffsetZ");
			w.AddTo(hybrids, "OffsetW");
			return new NoiseNode(new NodeDescriptor("DomainOffset", nodeLookups: nodes,
				hybrids: hybrids));
		}

		public NoiseNode DomainRotate(float yaw = 0f, float pitch = 0f, float roll = 0f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Yaw", Bits(yaw) },
				{ "Pitch", Bits(pitch) },
				{ "Roll", Bits(roll) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("DomainRotate", vars, nodes));
		}

		public NoiseNode DomainAxisScale(float x = 1f, float y = 1f,
			float z = 1f, float w = 1f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "ScalingX", Bits(x) },
				{ "ScalingY", Bits(y) },
				{ "ScalingZ", Bits(z) },
				{ "ScalingW", Bits(w) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("DomainAxisScale", vars, nodes));
		}

		public NoiseNode DomainRotatePlane(
			PlaneRotationType type = PlaneRotationType.ImproveXYPlanes)
		{
			var vars = new Dictionary<string, int>
			{
				{ "RotationType", EnumIndex("DomainRotatePlane", "RotationType",
					type.ToMetadataString()) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("DomainRotatePlane", vars, nodes));
		}

		public NoiseNode SeedOffset(int offset = 0)
		{
			var vars = new Dictionary<string, int>
			{
				{ "SeedOffset", offset }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("SeedOffset", vars, nodes));
		}

		public NoiseNode Remap(Hybrid fromMin = default, Hybrid fromMax = default,
			Hybrid toMin = default, Hybrid toMax = default)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			fromMin.AddTo(hybrids, "FromMin");
			fromMax.AddTo(hybrids, "FromMax");
			toMin.AddTo(hybrids, "ToMin");
			toMax.AddTo(hybrids, "ToMax");
			return new NoiseNode(new NodeDescriptor("Remap", nodeLookups: nodes, hybrids: hybrids));
		}

		public NoiseNode ConvertRgba8(float min = -1f, float max = 1f)
		{
			var vars = new Dictionary<string, int>
			{
				{ "Min", Bits(min) },
				{ "Max", Bits(max) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("ConvertRGBA8", vars, nodes));
		}

		public NoiseNode Terrace(float stepCount = 4f, Hybrid smoothness = default)
		{
			var vars = new Dictionary<string, int>
			{
				{ "StepCount", Bits(stepCount) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			smoothness.AddTo(hybrids, "Smoothness");
			return new NoiseNode(new NodeDescriptor("Terrace", vars, nodes, hybrids));
		}

		public NoiseNode AddDimension(Hybrid newDimensionPosition = default)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			newDimensionPosition.AddTo(hybrids, "NewDimensionPosition");
			return new NoiseNode(new NodeDescriptor("AddDimension", nodeLookups: nodes,
				hybrids: hybrids));
		}

		public NoiseNode RemoveDimension(Dimension dimension = Dimension.W)
		{
			var vars = new Dictionary<string, int>
			{
				{ "RemoveDimension", EnumIndex("RemoveDimension", "RemoveDimension",
					dimension.ToMetadataString()) }
			};
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("RemoveDimension", vars, nodes));
		}

		public NoiseNode Cache()
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("GeneratorCache", nodeLookups: nodes));
		}

		public NoiseNode PingPong(Hybrid strength = default)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			strength.AddTo(hybrids, "PingPongStrength");
			return new NoiseNode(new NodeDescriptor("PingPong", nodeLookups: nodes,
				hybrids: hybrids));
		}

		public NoiseNode Abs()
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("Abs", nodeLookups: nodes));
		}

		public NoiseNode SignedSqrt()
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "Source", m_Descriptor }
			};
			return new NoiseNode(new NodeDescriptor("SignedSquareRoot", nodeLookups: nodes));
		}

		#endregion

		#region Operators

		// LHS=NodeLookup, RHS=Hybrid (for Add, Multiply)
		static NoiseNode BinaryOpNodeLhs(string nodeType, NoiseNode lhs, Hybrid rhs)
		{
			var nodes = new Dictionary<string, NodeDescriptor>
			{
				{ "LHS", lhs.m_Descriptor }
			};
			var hybrids = new Dictionary<string, HybridValue>();
			rhs.AddTo(hybrids, "RHS");
			return new NoiseNode(new NodeDescriptor(nodeType, nodeLookups: nodes,
				hybrids: hybrids));
		}

		// LHS=Hybrid, RHS=Hybrid (for Subtract, Divide, Modulus)
		static NoiseNode BinaryOpBothHybrid(string nodeType, NoiseNode lhs, Hybrid rhs)
		{
			var hybrids = new Dictionary<string, HybridValue>
			{
				{ "LHS", new HybridValue(lhs.m_Descriptor) }
			};
			rhs.AddTo(hybrids, "RHS");
			return new NoiseNode(new NodeDescriptor(nodeType, hybrids: hybrids));
		}

		// Add: LHS is GeneratorSource, RHS is HybridSource
		public static NoiseNode operator +(NoiseNode lhs, NoiseNode rhs) =>
			BinaryOpNodeLhs("Add", lhs, rhs);

		public static NoiseNode operator +(NoiseNode lhs, float rhs) =>
			BinaryOpNodeLhs("Add", lhs, rhs);

		public static NoiseNode operator +(float lhs, NoiseNode rhs) =>
			BinaryOpNodeLhs("Add", Noise.Constant(lhs), rhs);

		// Subtract: both HybridSource
		public static NoiseNode operator -(NoiseNode lhs, NoiseNode rhs) =>
			BinaryOpBothHybrid("Subtract", lhs, rhs);

		public static NoiseNode operator -(NoiseNode lhs, float rhs) =>
			BinaryOpBothHybrid("Subtract", lhs, rhs);

		public static NoiseNode operator -(float lhs, NoiseNode rhs) =>
			BinaryOpBothHybrid("Subtract", Noise.Constant(lhs), rhs);

		// Multiply: LHS is GeneratorSource, RHS is HybridSource
		public static NoiseNode operator *(NoiseNode lhs, NoiseNode rhs) =>
			BinaryOpNodeLhs("Multiply", lhs, rhs);

		public static NoiseNode operator *(NoiseNode lhs, float rhs) =>
			BinaryOpNodeLhs("Multiply", lhs, rhs);

		public static NoiseNode operator *(float lhs, NoiseNode rhs) =>
			BinaryOpNodeLhs("Multiply", Noise.Constant(lhs), rhs);

		// Divide: both HybridSource
		public static NoiseNode operator /(NoiseNode lhs, NoiseNode rhs) =>
			BinaryOpBothHybrid("Divide", lhs, rhs);

		public static NoiseNode operator /(NoiseNode lhs, float rhs) =>
			BinaryOpBothHybrid("Divide", lhs, rhs);

		public static NoiseNode operator /(float lhs, NoiseNode rhs) =>
			BinaryOpBothHybrid("Divide", Noise.Constant(lhs), rhs);

		// Modulus: both HybridSource
		public static NoiseNode operator %(NoiseNode lhs, NoiseNode rhs) =>
			BinaryOpBothHybrid("Modulus", lhs, rhs);

		public static NoiseNode operator %(NoiseNode lhs, float rhs) =>
			BinaryOpBothHybrid("Modulus", lhs, rhs);

		public static NoiseNode operator %(float lhs, NoiseNode rhs) =>
			BinaryOpBothHybrid("Modulus", Noise.Constant(lhs), rhs);

		// Negate: node * -1
		public static NoiseNode operator -(NoiseNode node) => node * -1f;

		#endregion
	}
}
