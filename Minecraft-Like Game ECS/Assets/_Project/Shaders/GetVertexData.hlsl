#pragma once

struct vertex
{
    uint4 data;
};

StructuredBuffer<vertex> vertices;
float3  chunk_origin;
int     vertex_count;

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
    if (vertex_id >= (uint)vertex_count) return;

    uint4 v = vertices[vertex_id].data;

    // ... DATA 1 and DATA 2 as previously defined ...
    float px = (float)(v.x & 0x3FF) / 10.0f;
    float py = (float)((v.x >> 10) & 0x3FF) / 10.0f;
    float pz = (float)((v.x >> 20) & 0x3FF) / 10.0f;
    uint ao_val = (v.x >> 30) & 0x3;
    float ao = ao_val / 3.0f; 
    position_ws = float3(px, py, pz) + chunk_origin;

    color = float4((v.y & 0xFF) / 255.0f, ((v.y >> 8) & 0xFF) / 255.0f, ((v.y >> 16) & 0xFF) / 255.0f, ((v.y >> 24) & 0xFF) / 255.0f);
    color.rgb *= ao; 

    // --- DATA 3: TexBase(9), TexOverlay(9), UV_X(10), NormalIndex(3), TangentSign(1) ---
    tex_base    = (float)(v.z & 0x1FF);
    tex_overlay = (float)((v.z >> 9) & 0x1FF);
    float uv_x  = (float)((v.z >> 18) & 0x3FF) / 10.0f;
    uint norm_idx = (v.z >> 28) & 0x7;
    normal_ws   = dir_vectors[norm_idx];
    float tan_sign = ((v.z >> 31) & 0x1) == 1 ? 1.0f : -1.0f;

    // --- DATA 4: TexNorm(9), TexSpec(9), UV_Y(10), TangentIndex(3) ---
    tex_normal   = (float)(v.w & 0x1FF);
    tex_specular = (float)((v.w >> 9) & 0x1FF);
    float uv_y   = (float)((v.w >> 18) & 0x3FF) / 10.0f;
    uint tan_idx = (v.w >> 28) & 0x7;
    tangent_ws   = dir_vectors[tan_idx] * tan_sign;
    uv = float2(uv_x, uv_y);
}