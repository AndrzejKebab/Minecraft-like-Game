using Unity.Entities;

namespace _Project.Tags
{
	public struct NeedsMeshSync : IComponentData { }
	
	public struct NeedsMeshRebuild : IComponentData { }
	
	public struct NeedsRebuild : IComponentData { }
	public struct NeedsColliderRebuild : IComponentData { }
}