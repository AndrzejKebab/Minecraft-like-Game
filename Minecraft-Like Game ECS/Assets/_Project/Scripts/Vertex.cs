using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	[BurstCompile]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 5)]
	public struct Vertex
	{
		public uint Data1;
		public uint Data2;
		public uint Data3;
		public uint Data4;
		public uint Data5;

		public Vertex(float3 pos, float3 norm, float3 tangent, Color32 color, float u, float v, uint tBase, uint tOverlay, uint tNorm, uint tSpec, int3 chunkCoord)
		{
			var posX    = (uint)math.round(math.clamp(pos.x * 16f, 0f, 1023f));
			var posY    = (uint)math.round(math.clamp(pos.y * 16f, 0f, 1023f));
			var posZ    = (uint)math.round(math.clamp(pos.z * 16f, 0f, 1023f));

			Data1 = posX | (posY << 10) | (posZ << 20);

			Data2 = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

			var uvX = (uint)math.round(math.clamp(u * 256f, 0f, 511f));
			var uvY = (uint)math.round(math.clamp(v * 256f, 0f, 511f));

			var normIdx = DirToIndex(norm);
			var tanIdx  = DirToIndex(tangent.xyz);

			Data3 = (tBase & 0x3FFu) | ((tOverlay & 0x3FFu) << 10) | (uvX << 20) | (normIdx << 29);
			Data4 = (tNorm & 0x3FFu) | ((tSpec    & 0x3FFu) << 10) | (uvY << 20) | (tanIdx  << 29);

			// Pack chunk grid coordinates into data5 (10 bits each, biased by +512).
			// The shader decodes these to reconstruct world-space chunk origin,
			// eliminating any dependency on SV_InstanceID or Unity's indirect draw system.
			// Supports chunk grid coords -512..+511 per axis (world ±16 384 units).
			var cx    = (uint)(chunkCoord.x + 512) & 0x3FFu;
			var cy    = (uint)(chunkCoord.y + 512) & 0x3FFu;
			var cz    = (uint)(chunkCoord.z + 512) & 0x3FFu;
			Data5 = cx | (cy << 10) | (cz << 20);
		}
		
		private static uint DirToIndex(float3 dir)
		{
			return dir.z switch
			       {
				       < -0.5f => 0,
				       > 0.5f  => 1,
				       _ => dir.y switch
				            {
					            > 0.5f  => 2,
					            < -0.5f => 3,
					            _       => dir.x < -0.5f ? 4 : (uint)5
				            }
			       };
		}
	}
}