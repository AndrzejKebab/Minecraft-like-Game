#include "Vertex.hlsl"

struct FaceData
{
    int3 Normal;
    Vertex Verticies[];
};

struct Voxel
{
    bool IsSolid;
    bool IsTransparent;
    bool IsFluid;
    FaceData FaceDatas[6];
};