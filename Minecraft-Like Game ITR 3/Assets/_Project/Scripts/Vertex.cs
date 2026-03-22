// ─────────────────────────────────────────────────────────────────────────────
//  Single-uint packed vertex
//
//  Bits  0– 5  pos.x      6 bits  (0–32)
//  Bits  6–11  pos.y      6 bits  (0–32)
//  Bits 12–17  pos.z      6 bits  (0–32)
//  Bits 18–20  faceIndex  3 bits  (0–5)  → normal + tangent decoded in shader
//  Bits 21–22  uvCorner   2 bits  (0–3)  → uv = float2(corner>>1, corner&1)
//  Bits 23–30  texIndex   8 bits  (0–255)→ Texture2DArray slice
//
//  Total: 31 bits — safely fits in uint32
// ─────────────────────────────────────────────────────────────────────────────

using System.Runtime.InteropServices;
using Unity.Burst;

[BurstCompile]
[StructLayout(LayoutKind.Sequential)]
public struct Vertex(int posX, int posY, int posZ, int faceIndex, int uvCorner, int texIndex)
{
	public uint Data = ((uint)posX & 0x3Fu) |
	                   (((uint)posY & 0x3Fu) << 6) |
	                   (((uint)posZ & 0x3Fu) << 12) |
	                   (((uint)faceIndex & 0x7u) << 18) |
	                   (((uint)uvCorner & 0x3u) << 21) |
	                   (((uint)texIndex & 0xFFu) << 23);
}