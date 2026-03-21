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
//    bits 18–20   faceIndex  (0–5)   → normal + tangent via lookup table
//    bits 21–22   uvCorner   (0–3)   → half2(corner >> 1, corner & 1)
//    bits 23–30   texIndex   (0–255) → Texture2DArray slice index
//
//  Output types match Unity Shader Graph vertex block inputs exactly:
//    position → half3   Object Space  (wire to Position slot,  space = Object)
//    normal   → half3   Object Space  (wire to Normal slot,    space = Object)
//    tangent  → half3   Object Space  (wire to Tangent slot,   space = Object)
//    uv       → half3   xy = uv 0/1, z = Texture2DArray slice (wire to UV0)
//
//  Because RenderPrimitivesIndexedIndirect submits with an identity matrix,
//  Object Space == World Space, so URP's built-in normal/tangent-to-world
//  transforms are no-ops and lighting resolves correctly.
// ─────────────────────────────────────────────────────────────────────────────

struct vertex
{
    uint data;
};

StructuredBuffer<vertex> vertices;

// Set per-chunk via MaterialPropertyBlock (World.ChunkPositionPropertyId)
float3 uChunkPosition;

// Face order matches VoxelData.cs:  Z-  Z+  Y+  Y-  X-  X+
static const float3 FaceNormals[6] =
{
    float3( 0,  0, -1), // 0  Z-
    float3( 0,  0,  1), // 1  Z+
    float3( 0,  1,  0), // 2  Y+
    float3( 0, -1,  0), // 3  Y-
    float3(-1,  0,  0), // 4  X-
    float3( 1,  0,  0), // 5  X+
};

static const float3 FaceTangents[6] =
{
    float3( 1,  0,  0), // 0  Z-
    float3(-1,  0,  0), // 1  Z+
    float3( 1,  0,  0), // 2  Y+
    float3(-1,  0,  0), // 3  Y-
    float3( 0,  0, -1), // 4  X-
    float3( 0,  0,  1), // 5  X+
};

// ─────────────────────────────────────────────────────────────────────────────
//  Outputs — wire every output into the Shader Graph VERTEX block, not fragment:
//
//    position → Position (Object Space)
//    normal   → Normal   (Object Space)   ← fixes dark lighting
//    tangent  → Tangent  (Object Space)   ← needed for normal maps
//    uv       → UV0                       ← uv.z carries the texture array index
// ─────────────────────────────────────────────────────────────────────────────
void get_vertex_data_float(float vertex_id,
                          out float3 position,
                          out float3 normal,
                          out float3 tangent,
                          out float3 uv)
{
    InitIndirectDrawArgs(0);
    uint d         = vertices[(uint)round(vertex_id)].data;

    uint px        =  d        & 0x3Fu;
    uint py        = (d >>  6) & 0x3Fu;
    uint pz        = (d >> 12) & 0x3Fu;
    uint face      = (d >> 18) & 0x7u;
    uint uvCorner  = (d >> 21) & 0x3u;
    uint texIndex  = (d >> 23) & 0xFFu;

    // Local voxel position + chunk world offset.
    // uChunkPosition is set per-draw via MaterialPropertyBlock.
    position = float3((px + uChunkPosition.x),
                      (py + uChunkPosition.y),
                      (pz + uChunkPosition.z));

    normal  = FaceNormals[face];
    tangent = FaceTangents[face];

    // uv.xy = one of the four (0,0) (0,1) (1,0) (1,1) corners
    // uv.z  = Texture2DArray slice index, pass to SAMPLE_TEXTURE2D_ARRAY as the slice param
    uv = float3(((uvCorner >> 1u) & 1u),
                ( uvCorner        & 1u),
                texIndex);
}
