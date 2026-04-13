using Unity.Entities;
using UnityEngine;

public class ChunkGfxBuffers : IComponentData, System.IDisposable
{
	public GraphicsBuffer        VertexBuffer; // StructuredBuffer<uint2> in shader
	public GraphicsBuffer        IndexBuffer;  // raw index buffer
	public GraphicsBuffer        ArgsBuffer;   // IndirectDrawIndexedArgs × 2 submeshes
	public MaterialPropertyBlock Mpb;
	public int                   SolidIndexCount;
	public int                   FluidIndexCount;

	public void Dispose()
	{
		VertexBuffer?.Release();
		IndexBuffer?.Release();
		ArgsBuffer?.Release();
		VertexBuffer = IndexBuffer = ArgsBuffer = null;
    
		SolidIndexCount = 0;
		FluidIndexCount = 0;
	}
}