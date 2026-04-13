#pragma once

struct vertex
{
    uint4 data;
};

StructuredBuffer<vertex> vertices;
float3 chunk_origin;
int vertex_count;

static const float3 dir_vectors[6] = {
    float3(0, 0, -1), float3(0, 0, 1),
    float3(0, 1, 0), float3(0, -1, 0),
    float3(-1, 0, 0), float3(1, 0, 0)
};

static const float3 tangent_vectors[6] = {
    float3(1, 0, 0), // 0: Back
    float3(-1, 0, 0), // 1: Front
    float3(1, 0, 0), // 2: Top
    float3(-1, 0, 0), // 3: Bottom
    float3(0, 0, -1), // 4: Left
    float3(0, 0, 1) // 5: Right
};

void get_vertex_data_float(
    uint vertex_id : SV_VertexID,
    out float3 position_ws,
    out float3 normal_ws,
    out float3 tangent_ws,
    out float2 uv,
    out float4 color,
    out float tex_base,
    out float tex_overlay,
    out float tex_normal,
    out float tex_specular
)
{
    if (vertex_id >= (uint)vertex_count) return;

    uint4 v = vertices[vertex_id].data;

    float px = (float)(v.x & 0x3FF) / 10.0f;
    float py = (float)((v.x >> 10) & 0x3FF) / 10.0f;
    float pz = (float)((v.x >> 20) & 0x3FF) / 10.0f;

    uint ao_val = (v.x >> 30) & 0x3;
    float ao = ao_val / 3.0f;
    position_ws = float3(px, py, pz) + chunk_origin;

    color = float4((v.y & 0xFF) / 255.0f, ((v.y >> 8) & 0xFF) / 255.0f, ((v.y >> 16) & 0xFF) / 255.0f,
                   ((v.y >> 24) & 0xFF) / 255.0f);
    color.rgb *= ao;

    tex_base = (float)(v.z & 0x1FF);
    tex_overlay = (float)((v.z >> 9) & 0x1FF);
    tex_normal = (float)((v.z >> 18) & 0x1FF);
    uint face_idx = (v.z >> 27) & 0x7;

    tex_specular = (float)(v.w & 0x1FF);

    normal_ws = dir_vectors[face_idx];
    tangent_ws = tangent_vectors[face_idx];

    // UV Reconstruction
    switch (face_idx)
    {
    case 0: uv = float2(-px, py);
        break; // Back  (-Z)
    case 1: uv = float2(px, py);
        break; // Front (+Z)
    case 2: uv = float2(px, pz);
        break; // Top   (+Y)
    case 3: uv = float2(px, -pz);
        break; // Bot   (-Y)
    case 4: uv = float2(pz, py);
        break; // Left  (-X)
    case 5: uv = float2(-pz, py);
        break; // Right (+X)
    default: uv = float2(0, 0);
        break;
    }
}