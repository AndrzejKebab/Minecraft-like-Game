using System.Runtime.InteropServices;
using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
	public struct Vertex
	{
		public uint4 Data1234; // 4× uint packed into 2× ulong for better GPU upload performance
		// PosX(10), PosY(10), PosZ(10), AO(2) = 32 bits
		// Color (32-bit RGBA)
		// TexBase(9), TexOverlay(9), TexNorm(9), FaceIndex(3), Padding(2) = 32 bits
		// TexSpec(9), Padding(23) = 32 bits

		/// <param name="faceIdx">Geometric face index — drives shader normal, tangent, UV reconstruction.</param>
		/// <param name="ao">Ambient occlusion value (0-3).</param>
		public Vertex(float3 pos, Block block, int faceIdx, int ao)
		{
			var px       = (uint)math.round(math.clamp(pos.x * 10f, 0f, 1023f));
			var py       = (uint)math.round(math.clamp(pos.y * 10f, 0f, 1023f));
			var pz       = (uint)math.round(math.clamp(pos.z * 10f, 0f, 1023f));
			var aoPacked = (uint)ao & 0x3;

			var data1 = px | (py << 10) | (pz << 20) | (aoPacked << 30);

			Color32 color = block.TintColor;
			var     data2 = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

			var tBase    = GetTextureIndex(faceIdx, block.BaseTextures) & 0x1FFu;
			var tOverlay = GetTextureIndex(faceIdx, block.OverlayTextures) & 0x1FFu;
			var tNorm    = GetTextureIndex(faceIdx, block.NormalTextures) & 0x1FFu;
			var tSpec    = GetTextureIndex(faceIdx, block.SpecularTextures) & 0x1FFu;

			var data3 = tBase | (tOverlay << 9) | (tNorm << 18) | (((uint)faceIdx & 0x7u) << 27);

			Data1234 = new uint4(data1, data2, data3, tSpec);
		}

		/// <param name="normalIdx">Geometric face index — drives shader normal, tangent, UV reconstruction.</param>
		/// <param name="textureFaceIdx">Logical face index after orientation remap — selects which texture slot to read.</param>
		/// <param name="ao">Ambient occlusion value (0-3).</param>
		public Vertex(float3 pos, Block block, int normalIdx, int textureFaceIdx, int ao)
		{
			var px       = (uint)math.round(math.clamp(pos.x * 10f, 0f, 1023f));
			var py       = (uint)math.round(math.clamp(pos.y * 10f, 0f, 1023f));
			var pz       = (uint)math.round(math.clamp(pos.z * 10f, 0f, 1023f));
			var aoPacked = (uint)ao & 0x3;

			var data1 = px | (py << 10) | (pz << 20) | (aoPacked << 30);

			Color32 color = block.TintColor;
			var     data2 = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

			// Textures sampled using logical (orientation-remapped) face; shader geometry uses normalIdx.
			var tBase    = GetTextureIndex(textureFaceIdx, block.BaseTextures) & 0x1FFu;
			var tOverlay = GetTextureIndex(textureFaceIdx, block.OverlayTextures) & 0x1FFu;
			var tNorm    = GetTextureIndex(textureFaceIdx, block.NormalTextures) & 0x1FFu;
			var tSpec    = GetTextureIndex(textureFaceIdx, block.SpecularTextures) & 0x1FFu;

			var data3 = tBase | (tOverlay << 9) | (tNorm << 18) | (((uint)normalIdx & 0x7u) << 27);

			Data1234 = new uint4(data1, data2, data3, tSpec);
		}

		private static ushort GetTextureIndex(int normalIdx, in NativeTexturesIDLayer layer)
		{
			return normalIdx switch
			       {
				       2 => layer.Top, 3  => layer.Bottom, 5 => layer.Right, 4 => layer.Left, 1 => layer.Front,
				       0 => layer.Back, _ => layer.Front
			       };
		}
	}
}