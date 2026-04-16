using Unity.Entities;

namespace _Project.Tags
{
	/// <summary> Chunk in view range. </summary>
	public struct IsInViewRange : IComponentData
	{
	}

	/// <summary> Chunk needs full population pass (terrain+caves+decoration). </summary>
	public struct NeedsPopulation : IComponentData
	{
	}

	/// <summary> Population finished. Block data valid. </summary>
	public struct IsPopulated : IComponentData
	{
	}

	/// <summary> Chunk has built mesh data (CPU side). </summary>
	public struct HasMesh : IComponentData
	{
	}

	/// <summary> Chunk has baked physics collider. </summary>
	public struct HasCollider : IComponentData
	{
	}

	/// <summary> Chunk has no solid blocks. Skip mesh + collider. </summary>
	public struct IsEmpty : IComponentData
	{
	}

	/// <summary> Chunk visible on screen, must be rendered. </summary>
	public struct NeedsRender : IComponentData
	{
	}

	/// <summary> Block data dirty, mesh must rebuild. </summary>
	public struct NeedsMeshSync : IComponentData
	{
	}

	/// <summary> Mesh rebuild must happen this frame regardless of priority. </summary>
	public struct UrgentMeshSync : IComponentData
	{
	}

	/// <summary> Mesh changed, collider must rebake. </summary>
	public struct NeedsColliderSync : IComponentData
	{
	}

	/// <summary> Collider rebake must happen this frame regardless of priority. </summary>
	public struct UrgentColliderSync : IComponentData
	{
	}

	/// <summary> Chunk slated for destruction. </summary>
	public struct MarkedToDestroy : IComponentData
	{
	}

	/// <summary> Mesh data ready, GPU upload pending. </summary>
	public struct MeshRequiresUpload : IComponentData
	{
	}

	/// <summary> Sort priority. Lower = higher priority (priority queue convention). </summary>
	public struct ChunkPriorityComponent : IComponentData
	{
		public float Distance;
		public float Importance;
		public float Priority => Distance * Importance;
	}
}