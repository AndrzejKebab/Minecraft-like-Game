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

	public struct HasRenderMesh : IComponentData
	{
	}
	
	public struct IsEmpty : IComponentData{}
	
	/// <summary>
	/// Tag – this chunk is in the render ring and should get a GPU mesh.
	/// Absent on outer-ring chunks that exist only to provide neighbour voxel data.
	/// </summary>
	public struct NeedsRender : IComponentData { }

	/// <summary>
	/// Float distance-to-player stored on the entity so ChunkPopulateSystem can
	/// process closer chunks first.
	/// </summary>
	public struct ChunkPriorityComponent : IComponentData
	{
		public float Distance;
	}
}