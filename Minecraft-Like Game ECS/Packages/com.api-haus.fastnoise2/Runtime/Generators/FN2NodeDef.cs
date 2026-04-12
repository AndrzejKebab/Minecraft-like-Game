using System;
using System.Collections.Generic;

namespace FastNoise2.Generators
{
	internal enum FN2MemberType
	{
		Float,
		Int,
		Enum,
		NodeLookup,
		Hybrid,
	}

	internal readonly struct FN2MemberDef
	{
		public readonly string Name;
		public readonly string LookupKey;
		public readonly FN2MemberType Type;
		public readonly int Index;
		public readonly string[] EnumValues;
		public readonly string Description;
		public readonly float DefaultFloat;
		public readonly int DefaultInt;
		public readonly float MinFloat;
		public readonly float MaxFloat;

		public FN2MemberDef(string name, FN2MemberType type, int index,
			string[] enumValues = null, string description = null,
			float defaultFloat = 0f, int defaultInt = 0,
			float minFloat = 0f, float maxFloat = 0f)
		{
			Name = name;
			LookupKey = name.Replace(" ", "").ToLower();
			Type = type;
			Index = index;
			EnumValues = enumValues;
			Description = description;
			DefaultFloat = defaultFloat;
			DefaultInt = defaultInt;
			MinFloat = minFloat;
			MaxFloat = maxFloat;
		}

		public bool TryGetEnumIndex(string enumValue, out int index)
		{
			index = -1;
			if (EnumValues == null)
				return false;
			string key = enumValue.Replace(" ", "");
			for (int i = 0; i < EnumValues.Length; i++)
			{
				if (string.Equals(EnumValues[i], key, StringComparison.OrdinalIgnoreCase))
				{
					index = i;
					return true;
				}
			}
			return false;
		}
	}

	internal sealed class FN2NodeDef
	{
		public readonly int Id;
		public readonly string NodeName;
		public readonly FN2MemberDef[] Members;
		public readonly string Description;
		public readonly string[] Groups;

		readonly Dictionary<string, FN2MemberDef> m_ByLookupKey;

		public FN2NodeDef(int id, string nodeName, FN2MemberDef[] members,
			string description = null, string[] groups = null)
		{
			Id = id;
			NodeName = nodeName;
			Members = members;
			Description = description;
			Groups = groups ?? Array.Empty<string>();
			m_ByLookupKey = new Dictionary<string, FN2MemberDef>(
				members.Length, StringComparer.OrdinalIgnoreCase);
			foreach (var m in members)
				m_ByLookupKey[m.LookupKey] = m;
		}

		public bool TryGetMember(string lookupKey, out FN2MemberDef member) =>
			m_ByLookupKey.TryGetValue(lookupKey, out member);

		public FN2MemberDef? GetMemberByTypeAndIndex(FN2MemberType type, int index)
		{
			foreach (var m in Members)
			{
				if (m.Type == type && m.Index == index)
					return m;
				// Variable types share the variable index space
				if (type == FN2MemberType.Float || type == FN2MemberType.Int ||
					type == FN2MemberType.Enum)
				{
					if ((m.Type == FN2MemberType.Float || m.Type == FN2MemberType.Int ||
							m.Type == FN2MemberType.Enum) && m.Index == index)
						return m;
				}
			}
			return null;
		}

		public int CountMembersOfType(FN2MemberType type)
		{
			int count = 0;
			foreach (var m in Members)
				if (m.Type == type)
					count++;
			return count;
		}

		public List<FN2MemberDef> GetMembersOfType(FN2MemberType type)
		{
			var result = new List<FN2MemberDef>();
			foreach (var m in Members)
				if (m.Type == type)
					result.Add(m);
			result.Sort((a, b) => a.Index.CompareTo(b.Index));
			return result;
		}

		/// <summary>
		/// Find the variable member (Float/Int/Enum) at the given variable index.
		/// </summary>
		public FN2MemberDef? FindVariableMemberByIndex(int index)
		{
			foreach (var m in Members)
			{
				if (m.Index == index && (m.Type == FN2MemberType.Float ||
						m.Type == FN2MemberType.Int || m.Type == FN2MemberType.Enum))
					return m;
			}
			return null;
		}

		/// <summary>
		/// Find the hybrid member at the given hybrid index.
		/// </summary>
		public FN2MemberDef? FindHybridMemberByIndex(int index)
		{
			foreach (var m in Members)
			{
				if (m.Index == index && m.Type == FN2MemberType.Hybrid)
					return m;
			}
			return null;
		}

		/// <summary>
		/// Count node lookups specifically (for the TypeLookup header).
		/// </summary>
		public int NodeLookupCount
		{
			get
			{
				int count = 0;
				foreach (var m in Members)
					if (m.Type == FN2MemberType.NodeLookup)
						count++;
				return count;
			}
		}
	}
}
