using System;
using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Decodes an FN2 base64 encoded node tree string into a <see cref="NodeDescriptor"/> tree.
	/// </summary>
	static class NodeDecoder
	{
		const byte TypeVariable = 0;
		const byte TypeLookup = 1;
		const byte TypeHybridLookup = 2;
		const byte TypeHybridVariable = 3;
		const byte NodeEndType = 4;

		public static NodeDescriptor Decode(string encodedNodeTree)
		{
			byte[] data = FnBase64.Decode(encodedNodeTree);
			if (data.Length == 0) return null;

			var stream = new DataStream(data);
			var referenceNodes = new List<NodeDescriptor>();

			return DeserialiseNodeDataInternal(stream, referenceNodes);
		}

		static NodeDescriptor DeserialiseNodeDataInternal(DataStream stream,
			List<NodeDescriptor> referenceNodes)
		{
			if (!stream.TryReadByte(out byte nodeId))
				return null;

			// 0xFF indicates a back-reference
			if (nodeId == 0xFF)
			{
				if (!stream.TryReadUInt16(out ushort refId))
					return null;
				if (refId >= referenceNodes.Count)
					return null;
				return referenceNodes[refId];
			}

			FN2NodeDef def = FN2NodeRegistry.GetNodeDefById(nodeId);
			if (def == null) return null;

			string nodeName = def.NodeName;

			var variables = new Dictionary<string, int>();
			var nodeLookups = new Dictionary<string, NodeDescriptor>();
			var hybrids = new Dictionary<string, HybridValue>();

			// Read first member lookup
			if (!stream.TryReadMemberLookup(out byte memberType, out byte memberIndex))
				return null;

			// Variables
			while (memberType == TypeVariable)
			{
				if (!stream.TryReadInt32(out int value))
					return null;

				var member = def.FindVariableMemberByIndex(memberIndex);
				if (member.HasValue)
					variables[member.Value.Name] = value;

				if (!stream.TryReadMemberLookup(out memberType, out memberIndex))
					return null;
			}

			// Node lookups
			if (memberType == TypeLookup)
			{
				int count = memberIndex;
				var lookupMembers = def.GetMembersOfType(FN2MemberType.NodeLookup);

				for (int i = 0; i < count; i++)
				{
					NodeDescriptor child = DeserialiseNodeDataInternal(stream, referenceNodes);
					if (i < lookupMembers.Count && child != null)
					{
						nodeLookups[lookupMembers[i].Name] = child;
					}
					// If child is null or extra, we still consumed the bytes
				}

				if (!stream.TryReadMemberLookup(out memberType, out memberIndex))
					return null;
			}

			// Hybrids
			while (memberType != NodeEndType)
			{
				if (memberType == TypeHybridLookup)
				{
					NodeDescriptor child = DeserialiseNodeDataInternal(stream, referenceNodes);
					var member = def.FindHybridMemberByIndex(memberIndex);
					if (member.HasValue && child != null)
						hybrids[member.Value.Name] = new HybridValue(child);
				}
				else if (memberType == TypeHybridVariable)
				{
					if (!stream.TryReadInt32(out int bits))
						return null;
					var member = def.FindHybridMemberByIndex(memberIndex);
					if (member.HasValue)
						hybrids[member.Value.Name] = new HybridValue(BitConverter.Int32BitsToSingle(bits));
				}

				if (!stream.TryReadMemberLookup(out memberType, out memberIndex))
					return null;
			}

			var descriptor = new NodeDescriptor(nodeName, variables, nodeLookups, hybrids);
			referenceNodes.Add(descriptor);
			return descriptor;
		}

		/// <summary>
		/// Streaming byte reader with NodeEnd stack decompression.
		/// </summary>
		class DataStream
		{
			readonly byte[] m_Data;
			int m_Pos;
			int m_NodeEndStack;

			public DataStream(byte[] data)
			{
				m_Data = data;
				m_Pos = 0;
				m_NodeEndStack = 0;
			}

			public bool TryReadByte(out byte value)
			{
				value = 0;
				if (m_Pos >= m_Data.Length) return false;
				value = m_Data[m_Pos++];
				return true;
			}

			public bool TryReadUInt16(out ushort value)
			{
				value = 0;
				if (m_Pos + 2 > m_Data.Length) return false;
				value = (ushort)(m_Data[m_Pos] | (m_Data[m_Pos + 1] << 8));
				m_Pos += 2;
				return true;
			}

			public bool TryReadInt32(out int value)
			{
				value = 0;
				if (m_NodeEndStack > 0) return false;
				if (m_Pos + 4 > m_Data.Length) return false;
				value = m_Data[m_Pos]
					| (m_Data[m_Pos + 1] << 8)
					| (m_Data[m_Pos + 2] << 16)
					| (m_Data[m_Pos + 3] << 24);
				m_Pos += 4;
				return true;
			}

			public bool TryReadMemberLookup(out byte type, out byte index)
			{
				type = 0;
				index = 0;

				if (m_NodeEndStack > 0)
				{
					m_NodeEndStack--;
					type = NodeEndType;
					return true;
				}

				if (m_Pos >= m_Data.Length) return false;
				byte b = m_Data[m_Pos++];
				type = (byte)(b & 0x7);
				index = (byte)((b >> 3) & 0x1F);

				if (type == NodeEndType)
				{
					m_NodeEndStack = index; // remaining ends after this one
				}

				return true;
			}
		}
	}
}
