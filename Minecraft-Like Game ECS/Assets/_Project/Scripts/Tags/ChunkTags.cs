using System;
using Unity.Entities;

namespace _Project.Tags
{
	/// <summary>
	/// Chunk is in view range.
	/// </summary>
	public struct IsInViewRange : IComponentData
	{
	}

	/// <summary>
	/// Mesh is fully populated.
	/// </summary>
	public struct IsPopulated : IComponentData
	{
	}

	/// <summary>
	/// Mesh has created collider.
	/// </summary>
	public struct HasCollider : IComponentData
	{
	}

	public struct HasMesh : IComponentData
	{
	}

	/// <summary>
	/// Chunk is full of air.
	/// </summary>
	public struct IsEmpty : IComponentData
	{
	}

	/// <summary>
	/// Chunk is visible on screen and needs to be rendered.
	/// </summary>
	public struct NeedsRender : IComponentData
	{
	}

	/// <summary>
	/// Chunk was modified and need mesh to rebuild.
	/// </summary>
	public struct NeedsMeshSync : IComponentData
	{
	}

	/// <summary>
	/// Chunk was modified and need mesh to rebuild immediately.
	/// </summary>
	public struct UrgentMeshSync : IComponentData
	{
	}

	/// <summary>
	/// Chunk was modified and need collider to rebuild.
	/// </summary>
	public struct NeedsColliderSync : IComponentData
	{
	}

	/// <summary>
	/// Chunk is no longer needed and should be destroyed as soon as possible.
	/// </summary>
	public struct MarkedToDestroy : IComponentData
	{
	}

	/// <summary>
	/// Use less tag needs to be deleted 
	/// </summary>
	[Obsolete]
	public struct NeedsTerrainTag : IComponentData
	{
	}
	/// <summary>
	/// Use less tag needs to be deleted 
	/// </summary>
	[Obsolete]
	public struct NeedsDecorationTag : IComponentData
	{
	}

	/// <summary>
	/// Chunk has mesh ready and needs to be uploaded to the GPU.
	/// </summary>
	public struct MeshRequiresUpload : IComponentData
	{
	}

	/// <summary>
	/// Used for sorting chunks by distance to player and importance to prioritize updates.
	/// </summary>
	public struct ChunkPriorityComponent : IComponentData
	{
		public float Distance;
		public float Importance;

		public float Priority => Distance * Importance;
	}
}