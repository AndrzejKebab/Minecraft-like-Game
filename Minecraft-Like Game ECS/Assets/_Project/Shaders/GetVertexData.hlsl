#ifndef BLOCK_VERTEX_UNPACK_INCLUDED
#define BLOCK_VERTEX_UNPACK_INCLUDED

static const float3 dir_vectors[6] =
{
    float3( 0,  0, -1), // Back  (idx 0)
    float3( 0,  0,  1), // Front (idx 1)
    float3( 0,  1,  0), // Top   (idx 2)
    float3( 0, -1,  0), // Bottom(idx 3)
    float3(-1,  0,  0), // Left  (idx 4)
    float3( 1,  0,  0)  // Right (idx 5)
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

    // 1/16-block grid — must match Vertex.POS_SCALE (16) in Vertex.cs.
    float px = (float)( data1        & 0x3FFu) / 16.0f;
    float py = (float)((data1 >> 10)  & 0x3FFu) / 16.0f;
    float pz = (float)((data1 >> 20)  & 0x3FFu) / 16.0f;

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

    // UVRotation packed at data4[9:10] — 0=none, 1=90°CCW, 2=180°, 3=90°CW
    uint uvRot = (data4 >> 9) & 0x3u;

    normalWS  = dir_vectors[faceIdx];
    tangentWS = tangent_vectors[faceIdx];

    // Rebuild face-local UV from world position.
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

    // Rotate UV within each tile cell. floor(uv) is the cell origin; frac(uv)-0.5
    // centres the rotation so it never bleeds into adjacent cells. Safe across
    // greedy-merged quads because each cell rotates independently.
    if (uvRot != 0u)
    {
        float2 c = frac(uv) - 0.5;
        float2 r;
        if      (uvRot == 1u) r = float2(-c.y,  c.x); // 90° CCW
        else if (uvRot == 2u) r = float2(-c.x, -c.y); // 180°
        else                  r = float2( c.y, -c.x); // 90° CW
        uv = floor(uv) + r + 0.5;
    }
}
#endif
