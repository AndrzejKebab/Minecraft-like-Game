using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Components
{
	public struct ChunkComponent : IComponentData
	{
		public NativeArray<ushort> BlockData;
	}

	/// <summary>
	///     Chunk's position in chunk-grid space. WorldPosition = ChunkCoord * CHUNK_SIZE.
	/// </summary>
	public struct ChunkPositionComponent : IComponentData
	{
		public int3 ChunkCoord;
		public int3 WorldPosition => ChunkCoord * VoxelData.CHUNK_SIZE;
	}

	public struct ChunkMapSingleton : IComponentData
	{
		public NativeHashMap<int3, Entity> ChunkMap;
	}
}