using Unity.Burst;
using Unity.Collections;

namespace PatataStudio.World.Mesh
{
    [BurstCompile]
    public struct MeshBuffer
    {
        public NativeList<Vertex> VertexBuffer;
        public NativeList<int> IndexBuffer;
        public NativeList<int> IndexBuffer2;

        internal void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            IndexBuffer2.Dispose();
        }
    }
}