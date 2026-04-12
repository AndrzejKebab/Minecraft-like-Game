#pragma once

static const float3 dir_vectors[6] = {
    float3(0, 0, -1), float3(0, 0, 1),
    float3(0, 1, 0),  float3(0, -1, 0),
    float3(-1, 0, 0), float3(1, 0, 0)
};

// In Shader Graph, pass your Vertex Color, and two custom UV streams (e.g., TEXCOORD0, TEXCOORD1) into this!
void get_vertex_data_float(
    float packed_color,
    float packed_data3,
    float packed_data4,
    out float4 color,       
    out float  tex_base,
    out float  tex_overlay,
    out float  tex_normal,
    out float  tex_specular,
    out float2 uv,
    out float3 normal_ws,
    out float3 tangent_ws
)
{
    uint c = asuint(packed_color);
    uint d3 = asuint(packed_data3);
    uint d4 = asuint(packed_data4);

    // AO is packed in the top 2 bits of Data4
    uint ao_val = (d4 >> 30) & 0x3;
    float ao = ao_val / 3.0f; 

    // Color
    color = float4((c & 0xFF) / 255.0f, ((c >> 8) & 0xFF) / 255.0f, ((c >> 16) & 0xFF) / 255.0f, ((c >> 24) & 0xFF) / 255.0f);
    color.rgb *= ao; 

    // Data 3
    tex_base    = (float)(d3 & 0x1FF);
    tex_overlay = (float)((d3 >> 9) & 0x1FF);
    float uv_x  = (float)((d3 >> 18) & 0x3FF) / 10.0f;
    uint norm_idx = (d3 >> 28) & 0x7;
    normal_ws   = dir_vectors[norm_idx];
    float tan_sign = ((d3 >> 31) & 0x1) == 1 ? 1.0f : -1.0f;

    // Data 4
    tex_normal   = (float)(d4 & 0x1FF);
    tex_specular = (float)((d4 >> 9) & 0x1FF);
    float uv_y   = (float)((d4 >> 18) & 0x3FF) / 10.0f;
    uint tan_idx = (d4 >> 28) & 0x7;
    tangent_ws   = dir_vectors[tan_idx] * tan_sign;

    uv = float2(uv_x, uv_y);
}