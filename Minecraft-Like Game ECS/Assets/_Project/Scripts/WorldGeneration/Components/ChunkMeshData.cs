using System;
using Unity.Entities;
using UnityEngine;

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
	
	public class ChunkRendererData : IComponentData, IDisposable
	{
		public Mesh                              Mesh;
		public UnityEngine.Rendering.BatchMeshID BatchMeshID;
		public int                               SolidIndexCount;
		public int                               FluidIndexCount;

		public void Dispose()
		{
			if (Mesh != null) UnityEngine.Object.Destroy(Mesh);
			Mesh = null;
		}
	}
}