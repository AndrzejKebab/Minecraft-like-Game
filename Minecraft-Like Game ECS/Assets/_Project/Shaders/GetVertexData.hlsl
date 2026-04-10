#pragma once

struct Vertex
{
    uint2 Position;   // packed half4 (8 bytes)
    uint2 Normal;     // packed half4 (8 bytes)
    uint2 Tangent;    // packed half4 (8 bytes)
    uint  Color;      // packed Color32 RGBA (4 bytes)
    uint  UVs;        // packed half2 (4 bytes)
    uint2 TextureIDs; // packed half4 (8 bytes)
};

StructuredBuffer<Vertex> _Vertices;
float3  _ChunkOrigin;
int     _VertexCount;

inline float2 unpack_half2(uint v)
{
    return float2(f16tof32(v), f16tof32(v >> 16));
}

inline float4 unpack_half4(uint2 v)
{
    return float4(unpack_half2(v.x), unpack_half2(v.y));
}

void get_vertex_data_float(
    uint    vertex_id : SV_VertexID,
    out float3 PositionWS,
    out float3 NormalWS,
    out float3 TangentWS,   
    out float2 UV,
    out float  TexBase,
    out float  TexOverlay,
    out float  TexNormal,
    out float  TexSpecular
)
{
#if defined(SHADERGRAPH_PREVIEW)
    PositionWS  = _ChunkOrigin;
    NormalWS    = float3(0, 1, 0);
    TangentWS   = float3(1, 0, 0);
    UV          = float2(0, 0);
    TexBase     = 0; TexOverlay = 0; TexNormal = 0; TexSpecular = 0;
    return;
#endif

    if (vertex_id >= (uint)_VertexCount)
    {
        PositionWS  = _ChunkOrigin;
        NormalWS    = float3(0, 1, 0);
        TangentWS   = float3(1, 0, 0);
        UV          = float2(0, 0);
        TexBase     = 0; TexOverlay = 0; TexNormal = 0; TexSpecular = 0;
        return;
    }

    Vertex v = _Vertices[vertex_id];

    float4 pos = unpack_half4(v.Position);
    float4 norm = unpack_half4(v.Normal);
    float4 tan = unpack_half4(v.Tangent);
    float2 uvs = unpack_half2(v.UVs);
    float4 texIds = unpack_half4(v.TextureIDs);

    PositionWS  = pos.xyz + _ChunkOrigin;
    NormalWS    = norm.xyz;
    TangentWS   = tan.xyz;
    UV          = uvs;
    TexBase     = texIds.x;
    TexOverlay  = texIds.y;
    TexNormal   = texIds.z;
    TexSpecular = texIds.w;
}

void get_vertex_data_half(
    uint   vertex_id : SV_VertexID,
    out half3 PositionWS,
    out half3 NormalWS,
    out half3 TangentWS,
    out half2 UV,
    out half  TexBase,
    out half  TexOverlay,
    out half  TexNormal,
    out half  TexSpecular
)
{
    float3 p; float3 n; float3 t; float2 uv;
    float tb, tov, tn, ts;
    get_vertex_data_float(vertex_id, p, n, t, uv, tb, tov, tn, ts);
    PositionWS = (half3)p;
    NormalWS   = (half3)n;
    TangentWS  = (half3)t;
    UV         = (half2)uv;
    TexBase    = (half)tb;
    TexOverlay = (half)tov;
    TexNormal  = (half)tn;
    TexSpecular = (half)ts;
}