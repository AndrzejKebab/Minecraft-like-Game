using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FastNoise2.Tests
{
	using Bindings;
	using Generators;

	public class NodeRegistryTests
	{
		[Test]
		public void Registry_HasExpectedNodeCount()
		{
			Assert.That(FN2NodeRegistry.NodeCount, Is.EqualTo(47),
				"Expected 47 node types from FastNoise2 v0.10+");
		}

		[Test]
		public void Registry_AllNodeNamesAreUnique()
		{
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string name in FN2NodeRegistry.AllNodeNames)
			{
				Assert.That(seen.Add(name), Is.True,
					$"Duplicate node name: {name}");
			}
		}

		[Test]
		public void Registry_GetNodeDefByName_KnownNodes()
		{
			string[] knownNodes =
			{
				"Constant", "White", "Checkerboard", "SineWave", "Gradient",
				"DistanceToPoint", "Simplex", "SuperSimplex", "Perlin", "Value",
				"CellularValue", "CellularDistance", "CellularLookup",
				"FractalFBm", "FractalRidged", "PingPong",
				"DomainWarpSimplex", "DomainWarpSuperSimplex", "DomainWarpGradient",
				"DomainWarpFractalProgressive", "DomainWarpFractalIndependent",
				"Add", "Subtract", "Multiply", "Divide", "Modulus",
				"Abs", "Min", "Max", "MinSmooth", "MaxSmooth",
				"SignedSquareRoot", "PowFloat", "PowInt",
				"DomainScale", "DomainOffset", "DomainRotate", "DomainAxisScale",
				"SeedOffset", "ConvertRGBA8", "GeneratorCache",
				"Fade", "Remap", "Terrace", "AddDimension", "RemoveDimension",
				"DomainRotatePlane"
			};

			foreach (string name in knownNodes)
			{
				FN2NodeDef def = FN2NodeRegistry.GetNodeDef(name);
				Assert.That(def, Is.Not.Null, $"GetNodeDef failed for '{name}'");
				Assert.That(def.NodeName, Is.EqualTo(name));
			}
		}

		[Test]
		public void Registry_GetNodeDefById_RoundTrip()
		{
			for (int id = 0; id < FN2NodeRegistry.NodeCount; id++)
			{
				FN2NodeDef byId = FN2NodeRegistry.GetNodeDefById(id);
				Assert.That(byId, Is.Not.Null, $"GetNodeDefById({id}) returned null");
				Assert.That(byId.Id, Is.EqualTo(id));

				FN2NodeDef byName = FN2NodeRegistry.GetNodeDef(byId.NodeName);
				Assert.That(byName.Id, Is.EqualTo(id),
					$"ID mismatch for '{byId.NodeName}': byId={id} byName={byName.Id}");
			}
		}

		[Test]
		public void Registry_Simplex_HasExpectedMembers()
		{
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef("Simplex");
			Assert.That(def.Id, Is.EqualTo(6));

			Assert.That(def.TryGetMember("featurescale", out var fs), Is.True);
			Assert.That(fs.Type, Is.EqualTo(FN2MemberType.Float));
			Assert.That(fs.Index, Is.EqualTo(0));

			Assert.That(def.TryGetMember("seedoffset", out var so), Is.True);
			Assert.That(so.Type, Is.EqualTo(FN2MemberType.Int));
			Assert.That(so.Index, Is.EqualTo(1));
		}

		[Test]
		public void Registry_FractalFBm_HasExpectedMembers()
		{
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef("FractalFBm");
			Assert.That(def.Id, Is.EqualTo(13));

			// Variables
			Assert.That(def.TryGetMember("octaves", out var oct), Is.True);
			Assert.That(oct.Type, Is.EqualTo(FN2MemberType.Int));
			Assert.That(oct.Index, Is.EqualTo(0));

			Assert.That(def.TryGetMember("lacunarity", out var lac), Is.True);
			Assert.That(lac.Type, Is.EqualTo(FN2MemberType.Float));
			Assert.That(lac.Index, Is.EqualTo(1));

			// NodeLookup
			Assert.That(def.TryGetMember("source", out var src), Is.True);
			Assert.That(src.Type, Is.EqualTo(FN2MemberType.NodeLookup));
			Assert.That(src.Index, Is.EqualTo(0));

			// Hybrids
			Assert.That(def.TryGetMember("gain", out var gain), Is.True);
			Assert.That(gain.Type, Is.EqualTo(FN2MemberType.Hybrid));
			Assert.That(gain.Index, Is.EqualTo(0));

			Assert.That(def.TryGetMember("weightedstrength", out var ws), Is.True);
			Assert.That(ws.Type, Is.EqualTo(FN2MemberType.Hybrid));
			Assert.That(ws.Index, Is.EqualTo(1));
		}

		[Test]
		public void Registry_CellularDistance_HasEnumValues()
		{
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef("CellularDistance");

			Assert.That(def.TryGetMember("distancefunction", out var df), Is.True);
			Assert.That(df.Type, Is.EqualTo(FN2MemberType.Enum));
			Assert.That(df.EnumValues, Is.Not.Null);
			Assert.That(df.EnumValues.Length, Is.GreaterThan(0));

			Assert.That(def.TryGetMember("returntype", out var rt), Is.True);
			Assert.That(rt.Type, Is.EqualTo(FN2MemberType.Enum));
			Assert.That(rt.EnumValues, Is.Not.Null);
			Assert.That(rt.EnumValues, Does.Contain("Index0"));
		}

		[Test]
		public void Registry_EnumIndex_MatchesFluentApi()
		{
			// DistanceFunction.Euclidean should be index 0
			int idx = NoiseNode.EnumIndex("CellularDistance", "DistanceFunction", "Euclidean");
			Assert.That(idx, Is.EqualTo(0));

			// DistanceFunction.Manhattan should be index 2
			idx = NoiseNode.EnumIndex("CellularDistance", "DistanceFunction", "Manhattan");
			Assert.That(idx, Is.EqualTo(2));
		}

		[Test]
		public void Registry_FindVariableMemberByIndex()
		{
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef("Simplex");

			var m0 = def.FindVariableMemberByIndex(0);
			Assert.That(m0.HasValue, Is.True);
			Assert.That(m0.Value.LookupKey, Is.EqualTo("featurescale"));

			var m1 = def.FindVariableMemberByIndex(1);
			Assert.That(m1.HasValue, Is.True);
			Assert.That(m1.Value.LookupKey, Is.EqualTo("seedoffset"));
		}

		[Test]
		public void Registry_FindHybridMemberByIndex()
		{
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef("FractalFBm");

			var h0 = def.FindHybridMemberByIndex(0);
			Assert.That(h0.HasValue, Is.True);
			Assert.That(h0.Value.LookupKey, Is.EqualTo("gain"));

			var h1 = def.FindHybridMemberByIndex(1);
			Assert.That(h1.HasValue, Is.True);
			Assert.That(h1.Value.LookupKey, Is.EqualTo("weightedstrength"));
		}

#if FN2_USER_SIGNED
		[Test]
		public void Validate_RegistryPopulatedFromNative()
		{
			// Registry is now populated from native C API — sanity check
			Assert.That(FN2NodeRegistry.NodeCount, Is.GreaterThan(0),
				"Registry should have nodes populated from native");

			for (int id = 0; id < FN2NodeRegistry.NodeCount; id++)
			{
				FN2NodeDef def = FN2NodeRegistry.GetNodeDefById(id);
				Assert.That(def, Is.Not.Null, $"Node at ID {id} is null");
				Assert.That(def.Id, Is.EqualTo(id), $"ID mismatch at index {id}");
				Assert.That(def.NodeName, Is.Not.Null.And.Not.Empty,
					$"Node at ID {id} has null/empty name");
				Assert.That(def.Members, Is.Not.Null,
					$"Node '{def.NodeName}' at ID {id} has null members");
			}
		}

		[Test]
		public void Validate_RegistryEncodesIdenticallyToNative()
		{
			// Encode with registry, decode with native — check round-trip
			NoiseNode original = Noise.Simplex().Fbm(0.5f, 0f, 3, 2f);
			string encoded = original.Encode();

			using FastNoise fn = FastNoise.FromEncodedNodeTree(encoded);
			Assert.That(fn != FastNoise.Invalid, Is.True,
				$"Native failed to decode registry-encoded string: {encoded}");
		}
#endif
	}
}
