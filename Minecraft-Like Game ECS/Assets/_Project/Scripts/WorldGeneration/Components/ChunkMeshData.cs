using System;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.WorldGeneration.Components
{
	public class ChunkMeshData : IComponentData, IDisposable
	{
		public Mesh ChunkMesh;

		public void Dispose()
		{
			if (ChunkMesh == null) return;
			Object.Destroy(ChunkMesh);
			ChunkMesh = null;
		}
	}
}