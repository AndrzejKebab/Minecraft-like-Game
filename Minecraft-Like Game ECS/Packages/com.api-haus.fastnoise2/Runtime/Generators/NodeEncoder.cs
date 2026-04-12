using System;
using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Encodes a <see cref="NodeDescriptor"/> tree into the FN2 binary format
	/// (compatible with the C++ NoiseTool), then base64-encodes with @ compression.
	/// </summary>
	static class NodeEncoder
	{
		// MemberLookup type values (bits 0-2)
		const byte TypeVariable = 0;
		const byte TypeLookup = 1;
		const byte TypeHybridLookup = 2;
		const byte TypeHybridVariable = 3;
		const byte NodeEnd = 4;

		public static string Encode(NodeDescriptor descriptor)
		{
			var stream = new List<byte>();
			int nodeEndStack = 0;
			var referenceIds = new Dictionary<NodeDescriptor, ushort>(
				ReferenceEqualityComparer.Instance);

			if (!SerialiseNodeDataInternal(descriptor, stream, ref nodeEndStack, referenceIds))
				return "";

			FlushNodeEndStack(stream, ref nodeEndStack);
			return FnBase64.Encode(stream);
		}

		static bool SerialiseNodeDataInternal(NodeDescriptor descriptor,
			List<byte> stream, ref int nodeEndStack,
			Dictionary<NodeDescriptor, ushort> referenceIds)
		{
			// Check for back-reference to previously encoded node
			if (referenceIds.TryGetValue(descriptor, out ushort refId))
			{
				FlushNodeEndStack(stream, ref nodeEndStack);
				stream.Add(0xFF); // UINT8_MAX marker for reference
				AddUInt16(stream, ref nodeEndStack, refId);
				return true;
			}

			FN2NodeDef def = FN2NodeRegistry.GetNodeDef(descriptor.NodeName);

			// Node ID
			FlushNodeEndStack(stream, ref nodeEndStack);
			stream.Add((byte)def.Id);

			// Resolve member indices and emit variables
			foreach (var kv in descriptor.Variables)
			{
				string key = FormatLookup(kv.Key);
				if (!def.TryGetMember(key, out var member))
					throw new ArgumentException(
						$"Unknown variable '{kv.Key}' on '{descriptor.NodeName}'");

				AddMemberLookup(stream, ref nodeEndStack, TypeVariable, (byte)member.Index);
				AddInt32(stream, ref nodeEndStack, kv.Value);
			}

			// Count node lookups, emit TypeLookup header
			int nodeLookupCount = def.NodeLookupCount;
			if (nodeLookupCount > 0)
			{
				AddMemberLookup(stream, ref nodeEndStack, TypeLookup, (byte)nodeLookupCount);

				// Emit children in metadata order
				var lookupMembers = def.GetMembersOfType(FN2MemberType.NodeLookup);
				foreach (var member in lookupMembers)
				{
					string originalName = FindOriginalName(descriptor.NodeLookups, member.LookupKey);
					if (originalName != null && descriptor.NodeLookups.TryGetValue(originalName,
							out var child))
					{
						if (!SerialiseNodeDataInternal(child, stream, ref nodeEndStack,
								referenceIds))
							return false;
					}
					else
					{
						// Node lookup not provided — emit a Constant node as fallback
						FlushNodeEndStack(stream, ref nodeEndStack);
						FN2NodeDef constDef = FN2NodeRegistry.GetNodeDef("Constant");
						stream.Add((byte)constDef.Id);
						PushNodeEnd(ref nodeEndStack);
					}
				}
			}

			// Emit hybrids in metadata order
			var hybridMembers = def.GetMembersOfType(FN2MemberType.Hybrid);
			foreach (var member in hybridMembers)
			{
				string originalName = FindOriginalName(descriptor.Hybrids, member.LookupKey);
				if (originalName == null)
					continue; // hybrid not specified, use default

				var hv = descriptor.Hybrids[originalName];
				if (hv.IsNode)
				{
					AddMemberLookup(stream, ref nodeEndStack, TypeHybridLookup,
						(byte)member.Index);
					if (!SerialiseNodeDataInternal(hv.NodeValue, stream, ref nodeEndStack,
							referenceIds))
						return false;
				}
				else
				{
					AddMemberLookup(stream, ref nodeEndStack, TypeHybridVariable,
						(byte)member.Index);
					AddInt32(stream, ref nodeEndStack,
						BitConverter.SingleToInt32Bits(hv.FloatValue));
				}
			}

			// Mark end of node
			PushNodeEnd(ref nodeEndStack);

			referenceIds[descriptor] = (ushort)referenceIds.Count;
			return true;
		}

		static void FlushNodeEndStack(List<byte> stream, ref int nodeEndStack)
		{
			if (nodeEndStack > 0)
			{
				byte lookup = MakeMemberLookup(NodeEnd, (byte)(nodeEndStack - 1));
				stream.Add(lookup);
				nodeEndStack = 0;
			}
		}

		static void PushNodeEnd(ref int nodeEndStack)
		{
			if (++nodeEndStack >= 32)
			{
				// Can't accumulate more than 31 in 5-bit index
				// Shouldn't happen in practice, but handle gracefully
			}
		}

		static byte MakeMemberLookup(byte type, byte index) =>
			(byte)((type & 0x7) | ((index & 0x1F) << 3));

		static void AddMemberLookup(List<byte> stream, ref int nodeEndStack,
			byte type, byte index)
		{
			FlushNodeEndStack(stream, ref nodeEndStack);
			stream.Add(MakeMemberLookup(type, index));
		}

		static void AddInt32(List<byte> stream, ref int nodeEndStack, int value)
		{
			FlushNodeEndStack(stream, ref nodeEndStack);
			stream.Add((byte)(value & 0xFF));
			stream.Add((byte)((value >> 8) & 0xFF));
			stream.Add((byte)((value >> 16) & 0xFF));
			stream.Add((byte)((value >> 24) & 0xFF));
		}

		static void AddUInt16(List<byte> stream, ref int nodeEndStack, ushort value)
		{
			FlushNodeEndStack(stream, ref nodeEndStack);
			stream.Add((byte)(value & 0xFF));
			stream.Add((byte)((value >> 8) & 0xFF));
		}

		/// <summary>
		/// Find the original (case-preserving) key in a dictionary that matches a
		/// metadata member name (lowercased).
		/// </summary>
		static string FindOriginalName<T>(IReadOnlyDictionary<string, T> dict, string metaKey)
		{
			foreach (var key in dict.Keys)
			{
				if (FormatLookup(key) == metaKey)
					return key;
			}
			return null;
		}

		static string FormatLookup(string s) => s.Replace(" ", "").ToLower();

		/// <summary>
		/// Reference equality comparer for NodeDescriptor instances
		/// (for node deduplication via reference identity).
		/// </summary>
		sealed class ReferenceEqualityComparer : IEqualityComparer<NodeDescriptor>
		{
			public static readonly ReferenceEqualityComparer Instance = new();

			public bool Equals(NodeDescriptor x, NodeDescriptor y) =>
				ReferenceEquals(x, y);

			public int GetHashCode(NodeDescriptor obj) =>
				System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
		}
	}
}
