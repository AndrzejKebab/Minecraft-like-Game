using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Components
{
	/// <summary>
	/// Managed component that owns the Unity Mesh object and Entities Graphics
	/// batch IDs for a chunk. Replaces ChunkGfxBuffers.
	///
	/// The single Mesh has two sub-meshes:
	///   sub-mesh 0 → solid geometry  (rendered on the chunk entity itself)
	///   sub-mesh 1 → fluid geometry  (rendered on FluidEntity companion)
	/// </summary>
	public class ChunkManagedMesh : IComponentData
	{
		public Mesh        Mesh;
		public BatchMeshID MeshBatchID;

		/// <summary>Render-only entity for solid geometry (sub-mesh 0).</summary>
		public Entity SolidEntity;

		/// <summary>Render-only entity for fluid geometry (sub-mesh 1). Entity.Null when no fluid faces.</summary>
		public Entity FluidEntity;
	}
}
