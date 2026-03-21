struct vertex
{
    half4 position;
    half3 texcoord; //UV + TextureIndex
    half2 normal;   // packed normals 4 bytes = 2 halfs
    half2 tangent;  // packed tangents 4 bytes = 2 halfs
    
};

StructuredBuffer<vertex> vertices;
 
void get_vertex_data_half(half vertex_id, out half4 position, out half4 texcoord, out half3 normal, out half4 tangent)
{
    uint index = round(vertex_id);
    position = vertices[index].position;
    texcoord = vertices[index].texcoord;
    normal = vertices[index].normal;
    tangent = vertices[index].tangent;
}