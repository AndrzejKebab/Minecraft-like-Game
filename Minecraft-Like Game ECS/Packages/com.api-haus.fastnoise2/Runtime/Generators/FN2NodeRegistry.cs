using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FastNoise2.Bindings;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Single source of truth for all FastNoise2 node type metadata.
	/// Populated from the native C API at static init time.
	/// Node IDs and member indices match the C++ registration order.
	/// </summary>
	static class FN2NodeRegistry
	{
		static readonly FN2NodeDef[] s_NodeDefs;
		static readonly Dictionary<string, FN2NodeDef> s_ByName;
		static readonly string[] s_AllNodeNames;

		static FN2NodeRegistry()
		{
#if FN2_USER_SIGNED
			int count = FastNoise.fnGetMetadataCount();
			var defs = new List<FN2NodeDef>(count);

			for (int id = 0; id < count; id++)
			{
				string rawName = Marshal.PtrToStringAnsi(FastNoise.fnGetMetadataName(id));
				string nodeName = rawName.Replace(" ", "");

				// Node-level rich metadata
				string nodeDesc = Marshal.PtrToStringAnsi(FastNoise.fnGetMetadataDescription(id));
				int groupCount = FastNoise.fnGetMetadataGroupCount(id);
				var groups = new string[groupCount];
				for (int gi = 0; gi < groupCount; gi++)
					groups[gi] = Marshal.PtrToStringAnsi(FastNoise.fnGetMetadataGroupName(id, gi));

				int variableCount = FastNoise.fnGetMetadataVariableCount(id);
				int nodeLookupCount = FastNoise.fnGetMetadataNodeLookupCount(id);
				int hybridCount = FastNoise.fnGetMetadataHybridCount(id);

				var members = new List<FN2MemberDef>(variableCount + nodeLookupCount + hybridCount);

				// Variables (Float, Int, Enum)
				for (int vi = 0; vi < variableCount; vi++)
				{
					string memberRaw = Marshal.PtrToStringAnsi(
						FastNoise.fnGetMetadataVariableName(id, vi));
					string memberName = memberRaw.Replace(" ", "");

					int dimIdx = FastNoise.fnGetMetadataVariableDimensionIdx(id, vi);
					if (dimIdx >= 0)
						memberName += "XYZW"[dimIdx];

					int nativeType = FastNoise.fnGetMetadataVariableType(id, vi);
					FN2MemberType type = (FN2MemberType)nativeType;

					string[] enumValues = null;
					if (type == FN2MemberType.Enum)
					{
						int enumCount = FastNoise.fnGetMetadataEnumCount(id, vi);
						enumValues = new string[enumCount];
						for (int ei = 0; ei < enumCount; ei++)
						{
							string raw = Marshal.PtrToStringAnsi(
								FastNoise.fnGetMetadataEnumName(id, vi, ei));
							enumValues[ei] = raw.Replace(" ", "");
						}
					}

					string varDesc = Marshal.PtrToStringAnsi(
						FastNoise.fnGetMetadataVariableDescription(id, vi));
					float defFloat = FastNoise.fnGetMetadataVariableDefaultFloat(id, vi);
					int defInt = FastNoise.fnGetMetadataVariableDefaultIntEnum(id, vi);
					float minFloat = FastNoise.fnGetMetadataVariableMinFloat(id, vi);
					float maxFloat = FastNoise.fnGetMetadataVariableMaxFloat(id, vi);

					members.Add(new FN2MemberDef(memberName, type, vi, enumValues,
						varDesc, defFloat, defInt, minFloat, maxFloat));
				}

				// Node lookups
				for (int ni = 0; ni < nodeLookupCount; ni++)
				{
					string memberRaw = Marshal.PtrToStringAnsi(
						FastNoise.fnGetMetadataNodeLookupName(id, ni));
					string memberName = memberRaw.Replace(" ", "");

					int dimIdx = FastNoise.fnGetMetadataNodeLookupDimensionIdx(id, ni);
					if (dimIdx >= 0)
						memberName += "XYZW"[dimIdx];

					string nlDesc = Marshal.PtrToStringAnsi(
						FastNoise.fnGetMetadataNodeLookupDescription(id, ni));

					members.Add(new FN2MemberDef(memberName, FN2MemberType.NodeLookup, ni,
						description: nlDesc));
				}

				// Hybrids
				for (int hi = 0; hi < hybridCount; hi++)
				{
					string memberRaw = Marshal.PtrToStringAnsi(
						FastNoise.fnGetMetadataHybridName(id, hi));
					string memberName = memberRaw.Replace(" ", "");

					int dimIdx = FastNoise.fnGetMetadataHybridDimensionIdx(id, hi);
					if (dimIdx >= 0)
						memberName += "XYZW"[dimIdx];

					string hybDesc = Marshal.PtrToStringAnsi(
						FastNoise.fnGetMetadataHybridDescription(id, hi));
					float hybDefault = FastNoise.fnGetMetadataHybridDefault(id, hi);

					members.Add(new FN2MemberDef(memberName, FN2MemberType.Hybrid, hi,
						description: hybDesc, defaultFloat: hybDefault));
				}

				defs.Add(new FN2NodeDef(id, nodeName, members.ToArray(),
					nodeDesc, groups));
			}
#else
			var defs = new List<FN2NodeDef>(0);
#endif

			s_NodeDefs = defs.ToArray();
			s_ByName = new Dictionary<string, FN2NodeDef>(
				s_NodeDefs.Length, StringComparer.OrdinalIgnoreCase);
			s_AllNodeNames = new string[s_NodeDefs.Length];

			for (int i = 0; i < s_NodeDefs.Length; i++)
			{
				s_ByName[s_NodeDefs[i].NodeName] = s_NodeDefs[i];
				s_AllNodeNames[i] = s_NodeDefs[i].NodeName;
			}
		}

		public static FN2NodeDef GetNodeDef(string nodeTypeName)
		{
			if (s_ByName.TryGetValue(nodeTypeName, out var def))
				return def;
			throw new ArgumentException($"Unknown FN2 node type: {nodeTypeName}");
		}

		public static FN2NodeDef GetNodeDefById(int id)
		{
			if (id < 0 || id >= s_NodeDefs.Length)
				return null;
			return s_NodeDefs[id];
		}

		public static string[] AllNodeNames => s_AllNodeNames;

		public static int NodeCount => s_NodeDefs.Length;
	}
}
