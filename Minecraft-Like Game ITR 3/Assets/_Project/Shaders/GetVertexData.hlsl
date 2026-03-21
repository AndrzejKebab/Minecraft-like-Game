// ─────────────────────────────────────────────────────────────────────────────
//  GetVertexData.hlsl
//
//  Packed vertex layout (one uint per vertex, matches C# Vertex struct):
//
//    bits  0– 5   pos.x      (0–32)
//    bits  6–11   pos.y      (0–32)
//    bits 12–17   pos.z      (0–32)
//    bits 18–20   faceIndex  (0–5)   → normal + tangent via lookup table
//    bits 21–22   uvCorner   (0–3)   → float2(corner >> 1, corner & 1)
//    bits 23–30   texIndex   (0–255) → Texture2DArray slice index
// ─────────────────────────────────────────────────────────────────────────────

struct vertex
{
    uint data;
};

StructuredBuffer<vertex> vertices;

// Face order matches VoxelData.cs:  Z-  Z+  Y+  Y-  X-  X+
static const half3 FaceNormals[6] =
{
    half3(0, 0, -1), // 0  Z-
    half3(0, 0, 1), // 1  Z+
    half3(0, 1, 0), // 2  Y+
    half3(0, -1, 0), // 3  Y-
    half3(-1, 0, 0), // 4  X-
    half3(1, 0, 0), // 5  X+
};

static const half3 FaceTangents[6] =
{
    half3(1, 0, 0), // 0  Z-
    half3(-1, 0, 0), // 1  Z+
    half3(1, 0, 0), // 2  Y+
    half3(-1, 0, 0), // 3  Y-
    half3(0, 0, -1), // 4  X-
    half3(0, 0, 1), // 5  X+
};

// ─────────────────────────────────────────────────────────────────────────────
//  Outputs (matching your original signature):
//
//  position  half4   xyz = local integer voxel pos,  w = 0
//  texcoord  half4   xy  = uv (0 or 1 per corner),   z = texIndex,  w = 0
//  normal    half3   face normal  (one of ±X ±Y ±Z)
//  tangent   half4   xyz = face tangent,              w = -1 (bitangent sign)
// ─────────────────────────────────────────────────────────────────────────────
void get_vertex_data_half(half vertex_id, out half4 position, out half4 texcoord, out half3 normal, out half4 tangent)
{
    uint d = vertices[round(vertex_id)].data;

    uint px = d & 0x3Fu;
    uint py = (d >> 6) & 0x3Fu;
    uint pz = (d >> 12) & 0x3Fu;
    uint face = (d >> 18) & 0x7u;
    uint uv_corner = (d >> 21) & 0x3u;
    uint tex_index = (d >> 23) & 0xFFu;

    position = half4((half)px, (half)py, (half)pz, (half)0);

    half u = (half)((uv_corner >> 1u) & 1u); // 0 or 1
    half v = (half)(uv_corner & 1u); // 0 or 1
    texcoord = half4(u, v, (half)tex_index, (half)0);

    normal = FaceNormals[face];
    tangent = half4(FaceTangents[face], (half)-1); // w = bitangent sign
}
