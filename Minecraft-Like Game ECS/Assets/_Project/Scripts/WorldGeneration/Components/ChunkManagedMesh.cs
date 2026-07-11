using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Components
{
	/// <summary>
	///     Owns the Unity Mesh object (via UnityObjectRef — managed IComponentData is
	///     deprecated since Entities 6.6) and Entities Graphics batch IDs for a chunk.
	///     Replaces ChunkGfxBuffers.
	///     The single Mesh has two sub-meshes:
	///     sub-mesh 0 → solid geometry  (rendered on SolidEntity companion)
	///     sub-mesh 1 → fluid geometry  (rendered on FluidEntity companion)
	///     Value semantics: mutations must be written back with SetComponentData.
	/// </summary>
	public struct ChunkManagedMesh : IComponentData
	{
		/// <summary>Render-only entity for fluid geometry (sub-mesh 1). Entity.Null when no fluid faces.</summary>
		public Entity FluidEntity;

		public UnityObjectRef<Mesh> Mesh;
		public BatchMeshID          MeshBatchID;

		/// <summary>Render-only entity for solid geometry (sub-mesh 0).</summary>
		public Entity SolidEntity;
	}
}
