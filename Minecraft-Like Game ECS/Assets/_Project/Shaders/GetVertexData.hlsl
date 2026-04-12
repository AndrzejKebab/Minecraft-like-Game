#pragma once

struct vertex
{
    uint4 data;
};

StructuredBuffer<vertex> vertices;
float3  chunk_origin;
int     vertex_count;

// Lookup array replacing the need to pass massive float vectors
static const float3 dir_vectors[6] = {
    float3(0, 0, -1), 
    float3(0, 0, 1),
    float3(0, 1, 0), 
    float3(0, -1, 0),
    float3(-1, 0, 0),
    float3(1, 0, 0)
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
#if defined(SHADERGRAPH_PREVIEW)
    position_ws  = chunk_origin;
    normal_ws = float3(0, 1, 0);
    tangent_ws = float3(1, 0, 0);
    uv = float2(0, 0);
    color = float4(1, 1, 1, 1);
    tex_base = 0;
    tex_overlay = 0;
    tex_normal = 0;
    tex_specular = 0;
    return;
#endif

    if (vertex_id >= (uint)vertex_count)
    {
        position_ws  = chunk_origin;
        normal_ws = float3(0, 1, 0);
        tangent_ws = float3(1, 0, 0);
        uv = float2(0, 0);
        color = float4(1, 1, 1, 1);
        tex_base = 0;
        tex_overlay = 0;
        tex_normal = 0;
        tex_specular = 0; return;
    }

    uint4 v = vertices[vertex_id].data;

    // --- DATA 1: Position ---
    float px = (float)(v.x & 0x3FF) / 16.0f;
    float py = (float)((v.x >> 10) & 0x3FF) / 16.0f;
    float pz = (float)((v.x >> 20) & 0x3FF) / 16.0f;
    position_ws = float3(px, py, pz) + chunk_origin;

    // --- DATA 2: Color ---
    color = float4((v.y & 0xFF) / 255.0f, ((v.y >> 8) & 0xFF) / 255.0f, ((v.y >> 16) & 0xFF) / 255.0f, ((v.y >> 24) & 0xFF) / 255.0f);

    // --- DATA 3: Textures, UV_X, Normal ---
    tex_base    = (float)(v.z & 0x3FF);
    tex_overlay = (float)((v.z >> 10) & 0x3FF);
    float uv_x  = (float)((v.z >> 20) & 0x1FF) / 256.0f; // Decode fraction
    uint norm_idx = (v.z >> 29) & 0x7;
    normal_ws   = dir_vectors[norm_idx];

    // --- DATA 4: Textures, UV_Y, Tangent ---
    tex_normal   = (float)(v.w & 0x3FF);
    tex_specular = (float)((v.w >> 10) & 0x3FF);
    float uv_y   = (float)((v.w >> 20) & 0x1FF) / 256.0f; // Decode fraction
    uint tan_idx = (v.w >> 29) & 0x7;
    tangent_ws   = dir_vectors[tan_idx];

    uv = float2(uv_x, uv_y);
}

void get_vertex_data_half(
    uint   vertex_id : SV_VertexID,
    out half3 position_ws,
    out half3 normal_ws, 
    out half3 tangent_ws,
    out half2 uv, 
    out half4 color,        
    out half  tex_base, 
    out half  tex_overlay,
    out half  tex_normal,
    out half  tex_specular
)
{
    float3 p;
    float3 n;
    float3 t;
    float2 uv1;
    float4 c;
    float tb,
    tov,
    tn,
    ts;
    get_vertex_data_float(vertex_id, p, n, t, uv1, c, tb, tov, tn, ts);
    position_ws = (half3)p;
    normal_ws = (half3)n;
    tangent_ws = (half3)t;
    uv = (half2)uv1;
    color = (half4)c;
    tex_base = (half)tb;
    tex_overlay = (half)tov;
    tex_normal = (half)tn;
    tex_specular = (half)ts;
}