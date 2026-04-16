#ifndef BLOCK_VERTEX_UNPACK_INCLUDED
#define BLOCK_VERTEX_UNPACK_INCLUDED

static const float3 dir_vectors[6] =
{
    float3( 0,  0, -1), // Back
    float3( 0,  0,  1), // Front
    float3( 0,  1,  0), // Top
    float3( 0, -1,  0), // Bottom
    float3(-1,  0,  0), // Left
    float3( 1,  0,  0)  // Right
};

static const float3 tangent_vectors[6] =
{
    float3( 1,  0,  0),
    float3(-1,  0,  0),
    float3( 1,  0,  0),
    float3(-1,  0,  0),
    float3( 0,  0, -1),
    float3( 0,  0,  1)
};

void UnpackBlockVertex_float(
    float4 packedUV8,
    out float3 positionWS,
    out float3 normalWS,
    out float3 tangentWS,
    out float2 uv,
    out float4 color,
    out float texBase,
    out float texOverlay,
    out float texNormal,
    out float texSpecular,
    out float ao
)
{
    uint4 v = asuint(packedUV8);

    uint data1 = v.x;
    uint data2 = v.y;
    uint data3 = v.z;
    uint data4 = v.w;

    float px = (float)( data1        & 0x3FFu) / 10.0f;
    float py = (float)((data1 >> 10)  & 0x3FFu) / 10.0f;
    float pz = (float)((data1 >> 20)  & 0x3FFu) / 10.0f;

    uint aoVal = (data1 >> 30) & 0x3u;
    ao = (float)aoVal / 3.0f;

    positionWS = float3(px, py, pz);

    color = float4(
        (float)( data2        & 0xFFu) / 255.0f,
        (float)((data2 >> 8)  & 0xFFu) / 255.0f,
        (float)((data2 >> 16) & 0xFFu) / 255.0f,
        (float)((data2 >> 24) & 0xFFu) / 255.0f
    );

    texBase     = (float)( data3        & 0x1FFu);
    texOverlay  = (float)((data3 >> 9)  & 0x1FFu);
    texNormal   = (float)((data3 >> 18) & 0x1FFu);

    uint faceIdx = (data3 >> 27) & 0x7u;

    texSpecular = (float)(data4 & 0x1FFu);

    normalWS  = dir_vectors[faceIdx];
    tangentWS = tangent_vectors[faceIdx];

    // Rebuild a face-local UV.
    switch (faceIdx)
    {
        case 0: uv = float2(-px,  py); break; // Back  (-Z)
        case 1: uv = float2( px,  py); break; // Front (+Z)
        case 2: uv = float2( px,  pz); break; // Top   (+Y)
        case 3: uv = float2( px, -pz); break; // Bottom(-Y)
        case 4: uv = float2( pz,  py); break; // Left  (-X)
        case 5: uv = float2(-pz,  py); break; // Right (+X)
        default: uv = float2(0, 0);    break;
    }
}
#endif