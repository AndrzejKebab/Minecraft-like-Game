
#ifndef VOXEL_MESH_INFO
#define VOXEL_MESH_INFO

StructuredBuffer<int> Indices;
StructuredBuffer<float3> Vertices;
StructuredBuffer<float2> UVs;
 
void GetVertexData_float(float vertexId, out float3 position, out float2 texcoord)
{
    const int index = Indices[round(vertexId)];
    position = Vertices[index];
    texcoord = UVs[index];
}
#endif