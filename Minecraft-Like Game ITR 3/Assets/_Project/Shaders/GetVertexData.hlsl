#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
// ─────────────────────────────────────────────────────────────────────────────
//  GetVertexData.hlsl
//
//  Packed vertex layout (one uint per vertex, matches C# Vertex struct):
//
//    bits  0– 5   pos.x      (0–32)
//    bits  6–11   pos.y      (0–32)
//    bits 12–17   pos.z      (0–32)
//    bits 18–20   faceIndex  (0–5)   → normal via lookup table
//    bits 21–22   uvCorner   (0–3)   → float2(corner >> 1, corner & 1)
//    bits 23–30   texIndex   (0–255) → Texture2DArray slice index
//
//  Shader Graph wiring (all in VERTEX block):
//    position → Position  (Object Space)
//    normal   → Normal    (Object Space)
//    uv       → UV0       xy = uv corner, z = Texture2DArray slice
//
// ─────────────────────────────────────────────────────────────────────────────

struct vertex
{
    uint data;
};

StructuredBuffer<vertex> vertices;

// Set per-chunk via MaterialPropertyBlock (World.ChunkPositionPropertyId)
half3 uChunkPosition;

// Face order matches VoxelData.cs:  Z-  Z+  Y+  Y-  X-  X+
static const half3 FaceNormals[6] =
{
    half3( 0,  0, -1), // 0  Z-
    half3( 0,  0,  1), // 1  Z+
    half3( 0,  1,  0), // 2  Y+
    half3( 0, -1,  0), // 3  Y-
    half3(-1,  0,  0), // 4  X-
    half3( 1,  0,  0), // 5  X+
};

void get_vertex_data_float(float  vertex_id,
                           out float3 position,
                           out float3 normal,
                           out float3 uv)
{
    InitIndirectDrawArgs(0);
    uint d        = vertices[(uint)round(vertex_id)].data;

    uint px       =  d        & 0x3Fu;
    uint py       = (d >>  6) & 0x3Fu;
    uint pz       = (d >> 12) & 0x3Fu;
    uint face     = (d >> 18) & 0x7u;
    uint uvCorner = (d >> 21) & 0x3u;
    uint texIndex = (d >> 23) & 0xFFu;

    position = float3(px + uChunkPosition.x,
                      py + uChunkPosition.y,
                      pz + uChunkPosition.z);

    normal = FaceNormals[face];

    // uv.xy = one of (0,0) (0,1) (1,0) (1,1)
    // uv.z  = Texture2DArray slice index
    uv = float3((uvCorner >> 1u) & 1u,
                 uvCorner        & 1u,
                (float)texIndex);
}

void get_vertex_data_half(half  vertex_id,
                           out half3 position,
                           out half3 normal,
                           out half3 uv)
{
    InitIndirectDrawArgs(0);
    uint d        = vertices[(uint)round(vertex_id)].data;

    uint px       =  d        & 0x3Fu;
    uint py       = (d >>  6) & 0x3Fu;
    uint pz       = (d >> 12) & 0x3Fu;
    uint face     = (d >> 18) & 0x7u;
    uint uvCorner = (d >> 21) & 0x3u;
    uint texIndex = (d >> 23) & 0xFFu;

    position = half3(px + uChunkPosition.x,
                      py + uChunkPosition.y,
                      pz + uChunkPosition.z);

    normal = FaceNormals[face];

    // uv.xy = one of (0,0) (0,1) (1,0) (1,1)
    // uv.z  = Texture2DArray slice index
    uv = half3((uvCorner >> 1u) & 1u,
                 uvCorner        & 1u,
                (float)texIndex);
}