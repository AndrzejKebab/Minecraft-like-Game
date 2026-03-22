#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"

struct vertex
{
    float3 position;
    float3 normal;
    float3 uv;
};

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
    vertex vertex        = vertices[(uint)round(vertex_id)];
    
    position = vertex.position;
    normal = vertex.normal;
    uv = vertex.uv;
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