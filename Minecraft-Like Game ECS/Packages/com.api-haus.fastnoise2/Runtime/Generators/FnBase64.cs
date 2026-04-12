using System;
using System.Collections.Generic;

namespace FastNoise2.Generators
{
	/// <summary>
	/// Custom base64 codec matching the C++ FastNoise2 Base64 format.
	/// Uses '@' compression for runs of 3+ consecutive 'A' characters.
	/// </summary>
	static class FnBase64
	{
		const string kEncodingTable =
			"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

		static readonly byte[] kDecodingTable = BuildDecodingTable();

		static byte[] BuildDecodingTable()
		{
			byte[] table = new byte[256];
			for (int i = 0; i < 256; i++) table[i] = 64;
			for (int i = 0; i < 64; i++) table[kEncodingTable[i]] = (byte)i;
			table['='] = 0; // padding treated as 0
			return table;
		}

		public static string Encode(List<byte> data)
		{
			int inLen = data.Count;
			if (inLen == 0) return "";

			// Use a list for flexible @ compression
			var ret = new List<char>();
			int consecutiveAs = 0;

			void AppendChar(char c)
			{
				if (c == 'A')
				{
					consecutiveAs++;
					if (consecutiveAs <= 2)
					{
						ret.Add('A');
					}
					else if (consecutiveAs >= kEncodingTable.Length + 2)
					{
						// Max run reached, flush and restart
						ret[ret.Count - 2] = '@';
						ret[ret.Count - 1] = kEncodingTable[consecutiveAs - 3];
						ret.Add('A');
						consecutiveAs = 1;
					}
				}
				else
				{
					if (consecutiveAs >= 3)
					{
						ret[ret.Count - 2] = '@';
						ret[ret.Count - 1] = kEncodingTable[consecutiveAs - 3];
					}
					if (c != '\0')
						ret.Add(c);
					consecutiveAs = 0;
				}
			}

			int i = 0;
			for (; i < inLen - 2; i += 3)
			{
				AppendChar(kEncodingTable[(data[i] >> 2) & 0x3F]);
				AppendChar(kEncodingTable[((data[i] & 0x3) << 4) | ((data[i + 1] & 0xF0) >> 4)]);
				AppendChar(kEncodingTable[((data[i + 1] & 0xF) << 2) | ((data[i + 2] & 0xC0) >> 6)]);
				AppendChar(kEncodingTable[data[i + 2] & 0x3F]);
			}

			if (i < inLen)
			{
				AppendChar(kEncodingTable[(data[i] >> 2) & 0x3F]);
				if (i == inLen - 1)
				{
					AppendChar(kEncodingTable[(data[i] & 0x3) << 4]);
					AppendChar('=');
				}
				else
				{
					AppendChar(kEncodingTable[((data[i] & 0x3) << 4) | ((data[i + 1] & 0xF0) >> 4)]);
					AppendChar(kEncodingTable[(data[i + 1] & 0xF) << 2]);
				}
				AppendChar('=');
			}
			else
			{
				AppendChar('\0');
			}

			return new string(ret.ToArray());
		}

		public static byte[] Decode(string input)
		{
			if (string.IsNullOrEmpty(input)) return Array.Empty<byte>();

			// First pass: compute decompressed length
			int rawLen = input.Length;
			int decompLen = 0;

			for (int i = 0; i < rawLen; i++)
			{
				if (input[i] == '@')
				{
					i++;
					if (i >= rawLen) return Array.Empty<byte>();
					byte aExtra = kDecodingTable[input[i]];
					if (aExtra == 64) return Array.Empty<byte>();
					decompLen += aExtra + 3;
				}
				else
				{
					decompLen++;
				}
			}

			int outLen = decompLen / 4 * 3;
			if (outLen == 0 || decompLen % 4 != 0)
				return Array.Empty<byte>();

			if (input[rawLen - 1] == '=')
			{
				outLen--;
				if (rawLen >= 2 && input[rawLen - 2] == '=')
					outLen--;
			}

			byte[] output = new byte[outLen];
			int inIdx = 0, outIdx = 0;
			int consecutiveAs = 0;

			char NextChar()
			{
				if (consecutiveAs > 0)
				{
					consecutiveAs--;
					return 'A';
				}
				if (inIdx >= rawLen)
					return 'A';
				char c = input[inIdx++];
				if (c == '@')
				{
					byte extra = kDecodingTable[input[inIdx++]];
					consecutiveAs = extra + 2; // total A's = extra+3, we emit one now
					return 'A';
				}
				return c;
			}

			while (inIdx < rawLen || consecutiveAs > 0)
			{
				char c0 = NextChar();
				char c1 = NextChar();
				char c2 = NextChar();
				char c3 = NextChar();

				uint a = kDecodingTable[c0];
				uint b = kDecodingTable[c1];
				uint c = kDecodingTable[c2];
				uint d = kDecodingTable[c3];

				uint triple = (a << 18) | (b << 12) | (c << 6) | d;

				if (outIdx < outLen) output[outIdx++] = (byte)((triple >> 16) & 0xFF);
				if (outIdx < outLen) output[outIdx++] = (byte)((triple >> 8) & 0xFF);
				if (outIdx < outLen) output[outIdx++] = (byte)(triple & 0xFF);
			}

			return output;
		}
	}
}
