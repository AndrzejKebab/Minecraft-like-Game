using _Project.WorldGeneration.Blocks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Components
{
	public struct ChunkComponent : IComponentData
	{
		public NativeArray<BlockState> BlockData;
	}

	/// <summary> Chunk position in chunk-grid space. WorldPosition = ChunkCoord * CHUNK_SIZE. </summary>
	public struct ChunkPositionComponent : IComponentData
	{
		public int3 ChunkCoord;
		public int3 WorldPosition => ChunkCoord * VoxelData.CHUNK_SIZE;
	}

	public struct ChunkMapSingleton : IComponentData
	{
		public NativeHashMap<int3, Entity>           ChunkMap;
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;
	}

	/// <summary>
	/// Per-chunk in-flight job handle. Represents all pending writes to this chunk's
	/// BlockData OR ChunkMeshData. Consumers MUST .Complete() this before reading.
	/// Producers MUST overwrite (not aggregate) this when scheduling new work that
	/// supersedes prior work on the same data.
	/// </summary>
	public struct ChunkActiveJob : IComponentData
	{
		public JobHandle Handle;
	}
}