#pragma once

#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"

// Matches the C# Vertex struct exactly — 5 uints = 20 bytes.
// MegaBufferSystem must use stride sizeof(uint)*5 = 20.
struct vertex
{
    uint4 data;
    uint  chunk_coord; // chunkGridX(10) | chunkGridY(10) | chunkGridZ(10) | unused(2)
                       // each axis biased by +512, supporting grid coords -512..+511
                       // world origin = chunkGrid * CHUNK_SIZE (32)
};

// Kept for the culling compute shader; no longer used for vertex positioning.
struct ChunkData {
    float3 center;
    uint vertexOffset;
    uint indexOffset;
    uint solidIndexCount;
    uint fluidIndexCount;
    uint padding;
};

StructuredBuffer<vertex>    vertices;
StructuredBuffer<ChunkData> _ChunkData;

static const float3 dir_vectors[6] = {
    float3(0, 0, -1), float3(0, 0, 1),
    float3(0, 1, 0),  float3(0, -1, 0),
    float3(-1, 0, 0), float3(1, 0, 0)
};

void get_vertex_data_float(
    uint    vertex_id : SV_VertexID,
    out float3 position_ws,
    out float3 normal_ws,
    out float3 tangent_ws,
    out float2 uv,
    out float4 color,
    out float  tex_base,
    out float  tex_overlay,
    out float  tex_normal,
    out float  tex_specular
)
{
    InitIndirectDrawArgs(0);

    // vertex_id is a GLOBAL index into the mega vertex buffer.
    // Indices were baked with (localIndex + VertexOffset) in ChunkMeshBuilderSystem,
    // so this always points at the correct vertex regardless of platform.
    uint4 v      = vertices[vertex_id].data;
    uint  v_coord = vertices[vertex_id].chunk_coord;

#if defined(SHADERGRAPH_PREVIEW)
    position_ws  = float3(0, 0, 0);
    normal_ws    = float3(0, 1, 0);
    tangent_ws   = float3(1, 0, 0);
    uv           = float2(0, 0);
    color        = float4(1, 1, 1, 1);
    tex_base     = 0; tex_overlay = 0; tex_normal = 0; tex_specular = 0;
    return;
#endif

    // ── Chunk world-space origin ───────────────────────────────────────────────
    // Decode the three 10-bit fields (biased by +512) back to signed grid coords.
    // World origin = chunkGrid * CHUNK_SIZE.  CHUNK_SIZE = 32.
    // Supports chunk grid coords from -512 to +511 per axis (world ±16 384 units).
    int cx = (int)(v_coord         & 0x3FFu) - 512;
    int cy = (int)((v_coord >> 10) & 0x3FFu) - 512;
    int cz = (int)((v_coord >> 20) & 0x3FFu) - 512;
    float3 chunk_origin = float3(cx, cy, cz) * 32.0;

    // ── DATA 1: local position + tangent sign ──────────────────────────────────
    float px = (float)(v.x         & 0x3FFu) / 16.0;
    float py = (float)((v.x >> 10) & 0x3FFu) / 16.0;
    float pz = (float)((v.x >> 20) & 0x3FFu) / 16.0;
    position_ws = float3(px, py, pz) + chunk_origin;

    // ── DATA 2: vertex colour ──────────────────────────────────────────────────
    color = float4(
         (v.y        & 0xFFu) / 255.0,
        ((v.y >>  8) & 0xFFu) / 255.0,
        ((v.y >> 16) & 0xFFu) / 255.0,
        ((v.y >> 24) & 0xFFu) / 255.0);

    // ── DATA 3: base tex, overlay tex, UV.x, normal index ─────────────────────
    tex_base    = (float)(v.z         & 0x3FFu);
    tex_overlay = (float)((v.z >> 10) & 0x3FFu);
    float uv_x  = (float)((v.z >> 20) & 0x1FFu) / 256.0;
    uint norm_idx = (v.z >> 29) & 0x7u;
    normal_ws   = dir_vectors[norm_idx];

    // ── DATA 4: normal tex, specular tex, UV.y, tangent index ─────────────────
    tex_normal   = (float)(v.w         & 0x3FFu);
    tex_specular = (float)((v.w >> 10) & 0x3FFu);
    float uv_y   = (float)((v.w >> 20) & 0x1FFu) / 256.0;
    uint tan_idx = (v.w >> 29) & 0x7u;
    tangent_ws   = dir_vectors[tan_idx];

    uv = float2(uv_x, uv_y);
}