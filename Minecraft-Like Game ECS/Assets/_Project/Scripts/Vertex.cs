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
		public uint4 Data1234;
		// Data1: PosX(10), PosY(10), PosZ(10), AO(2)
		// Data2: Color RGBA (32)
		// Data3: TexBase(9), TexOverlay(9), TexNorm(9), FaceIndex(3), Padding(2)
		// Data4: TexSpec(9), UVRotation(2), Padding(21)

		/// <summary>
		/// Standard constructor. faceIdx drives shader normal/tangent/UV reconstruction.
		/// textureFaceIdx is the orientation-remapped slot for texture array lookup.
		/// uvRot: 0=none, 1=90°CCW, 2=180°, 3=90°CW — applied per-cell in shader.
		/// </summary>
		public Vertex(float3 pos, Block block, int faceIdx, int textureFaceIdx, int ao, int uvRot = 0)
		{
			var px       = (uint)math.round(math.clamp(pos.x * 10f, 0f, 1023f));
			var py       = (uint)math.round(math.clamp(pos.y * 10f, 0f, 1023f));
			var pz       = (uint)math.round(math.clamp(pos.z * 10f, 0f, 1023f));
			var aoPacked = (uint)ao & 0x3u;

			var data1 = px | (py << 10) | (pz << 20) | (aoPacked << 30);

			Color32 color = block.TintColor;
			var     data2 = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

			var tBase    = GetTextureIndex(textureFaceIdx, block.BaseTextures)     & 0x1FFu;
			var tOverlay = GetTextureIndex(textureFaceIdx, block.OverlayTextures)  & 0x1FFu;
			var tNorm    = GetTextureIndex(textureFaceIdx, block.NormalTextures)   & 0x1FFu;
			var tSpec    = GetTextureIndex(textureFaceIdx, block.SpecularTextures) & 0x1FFu;

			var data3 = tBase | (tOverlay << 9) | (tNorm << 18) | (((uint)faceIdx & 0x7u) << 27);
			var data4 = tSpec | (((uint)uvRot & 0x3u) << 9);

			Data1234 = new uint4(data1, data2, data3, data4);
		}

		private static ushort GetTextureIndex(int normalIdx, in NativeTexturesIDLayer layer)
		{
			return normalIdx switch
			{
				2 => layer.Top,
				3 => layer.Bottom,
				5 => layer.Right,
				4 => layer.Left,
				1 => layer.Front,
				0 => layer.Back,
				_ => layer.Front
			};
		}
	}
}