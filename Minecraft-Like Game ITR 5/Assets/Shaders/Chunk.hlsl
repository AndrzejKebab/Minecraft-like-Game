#define CHUNK_SIZE 32

struct Chunk
{
    int3 Position;
    uint VoxelMap[];
};

uint to1D(uint3 pos)
{
    return pos.x + CHUNK_SIZE * (pos.y + CHUNK_SIZE * pos.z);
}

int to1D(int3 pos)
{
    return pos.x + CHUNK_SIZE * (pos.y + CHUNK_SIZE * pos.z);
}

uint3 to3D(uint idx)
{
    uint x = idx % CHUNK_SIZE;
    uint y = (idx / CHUNK_SIZE) % CHUNK_SIZE;
    uint z = idx / (CHUNK_SIZE * CHUNK_SIZE);
    
    return uint3(x, y, z);
}