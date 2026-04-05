using Unity.Burst;
using Unity.Collections;

namespace _Project.WorldGeneration
{
	[BurstCompile]
	public struct NativeMesh
	{
		public NativeList<Vertex> Vertices;
		public NativeList<int>  Triangles;

		public NativeMesh(Allocator allocator)
		{
			Vertices = new NativeList<Vertex>(allocator);
			Triangles = new NativeList<int>(allocator);
		}
		
		public bool IsCreated => Vertices.IsCreated && Triangles.IsCreated;

		public void Dispose()
		{
			if (Vertices.IsCreated) Vertices.Dispose();
			if (Triangles.IsCreated) Triangles.Dispose();
		}
	}
}