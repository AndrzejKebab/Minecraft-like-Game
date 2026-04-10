using System;
using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	public struct ChunkMeshData : IComponentData, IDisposable
	{
		public NativeMesh SolidMesh;
		public NativeMesh FluidMesh;

		public void Dispose()
		{
			if (SolidMesh.IsCreated)
				SolidMesh.Dispose();
			if (FluidMesh.IsCreated)
				FluidMesh.Dispose();
		}
	}
}