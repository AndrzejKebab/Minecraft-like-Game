using System;
using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration.Components
{
	public class ChunkMeshData : IComponentData, IDisposable
	{
		public Mesh ChunkMesh;

		public void Dispose()
		{
			if (ChunkMesh == null) return;
			UnityEngine.Object.Destroy(ChunkMesh);
			ChunkMesh = null;
		}
	}
}