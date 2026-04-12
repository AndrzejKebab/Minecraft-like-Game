using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	public struct VoxelMeshAllocation : IComponentData 
	{
		public int  VertexOffset;
		public int  IndexOffset;
		public int  SolidIndexCount;
		public int  FluidIndexCount;
		public int  VertexCount;
		public bool IsAllocated;
	}
}