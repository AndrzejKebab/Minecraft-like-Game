using System;
using FastNoise2.Bindings;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Builds a native <see cref="FastNoise"/> handle tree from a <see cref="NodeDescriptor"/> tree.
	/// </summary>
	static class NodeBuilder
	{
		public static FastNoise Build(NodeDescriptor descriptor)
		{
#if FN2_USER_SIGNED
			FastNoise fn = new(descriptor.NodeName);
			FN2NodeDef def = FN2NodeRegistry.GetNodeDef(descriptor.NodeName);

			foreach (var kv in descriptor.Variables)
			{
				if (!def.TryGetMember(FormatLookup(kv.Key), out var member))
					throw new ArgumentException(
						$"Unknown member '{kv.Key}' on node '{descriptor.NodeName}'");

				switch (member.Type)
				{
					case FN2MemberType.Float:
						FastNoise.fnSetVariableFloat(fn.mNodeHandle, member.Index,
							BitConverter.Int32BitsToSingle(kv.Value));
						break;

					case FN2MemberType.Int:
					case FN2MemberType.Enum:
						FastNoise.fnSetVariableIntEnum(fn.mNodeHandle, member.Index, kv.Value);
						break;

					default:
						throw new ArgumentException(
							$"Member '{kv.Key}' on '{descriptor.NodeName}' is not a variable type");
				}
			}

			foreach (var kv in descriptor.NodeLookups)
			{
				if (!def.TryGetMember(FormatLookup(kv.Key), out var member))
					throw new ArgumentException(
						$"Unknown member '{kv.Key}' on node '{descriptor.NodeName}'");

				FastNoise child = Build(kv.Value);
				IntPtr childHandle = child.mNodeHandle;
				FastNoise.fnSetNodeLookup(fn.mNodeHandle, member.Index, childHandle);
			}

			foreach (var kv in descriptor.Hybrids)
			{
				if (!def.TryGetMember(FormatLookup(kv.Key), out var member))
					throw new ArgumentException(
						$"Unknown member '{kv.Key}' on node '{descriptor.NodeName}'");

				if (kv.Value.IsNode)
				{
					FastNoise child = Build(kv.Value.NodeValue);
					IntPtr childHandle = child.mNodeHandle;
					FastNoise.fnSetHybridNodeLookup(fn.mNodeHandle, member.Index, childHandle);
				}
				else
				{
					FastNoise.fnSetHybridFloat(fn.mNodeHandle, member.Index, kv.Value.FloatValue);
				}
			}

			return fn;
#else
			throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
		}

		static string FormatLookup(string s) => s.Replace(" ", "").ToLower();
	}
}
