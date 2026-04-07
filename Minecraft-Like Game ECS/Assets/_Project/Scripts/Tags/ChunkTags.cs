using Unity.Entities;

namespace _Project.Tags
{
	public struct IsVisible : IComponentData
	{
	}

	public struct IsPopulated : IComponentData
	{
	}

	public struct HasCollider : IComponentData
	{
	}

	public struct HasMesh : IComponentData
	{
	}

	public struct IsEmpty : IComponentData
	{
	}

	public struct NeedsRender : IComponentData
	{
	}

	public struct NeedsMeshSync : IComponentData
	{
	}

	public struct NeedsColliderSync : IComponentData
	{
	}

	public struct MarkedToDestroy : IComponentData
	{
	}

	/// <summary>
	///     Float distance-to-player stored on the entity so ChunkPopulateSystem can
	///     process closer chunks first.
	/// </summary>
	public struct ChunkPriorityComponent : IComponentData
	{
		public float Distance;
	}
}