using System;
using Unity.Collections;
using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	public struct ChunkMeshData : IComponentData, IDisposable
	{
		public NativeList<Vertex> CombinedVertices;
		public NativeList<int>    CombinedIndices;
		
		public int SolidVertexCount;
		public int SolidIndexCount;

		public void Dispose()
		{
			if (CombinedVertices.IsCreated) CombinedVertices.Dispose();
			if (CombinedIndices.IsCreated) CombinedIndices.Dispose();
		}
	}
}