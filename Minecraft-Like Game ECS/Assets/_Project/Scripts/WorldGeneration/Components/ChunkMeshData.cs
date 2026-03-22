using System;
using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	public struct ChunkMeshData : IComponentData, IDisposable
	{
		public NativeMesh SolidMesh;
		public NativeMesh TransparentMesh;
		public NativeMesh FluidMesh;

		public void Dispose()
		{
			SolidMesh.Dispose();
			TransparentMesh.Dispose();
			FluidMesh.Dispose();
		}
	}
}