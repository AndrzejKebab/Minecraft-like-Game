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
		public int3 WorldPosition => ChunkCoord * ChunkData.CHUNK_SIZE;
	}

	public struct ChunkMapSingleton : IComponentData
	{
		public NativeHashMap<int3, Entity>           ChunkMap;
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;
	}

	/// <summary>
	///     Per-chunk in-flight job handle. Represents all pending writes to this chunk's
	///     BlockData OR ChunkMeshData. Consumers MUST .Complete() this before reading.
	///     Producers MUST overwrite (not aggregate) this when scheduling new work that
	///     supersedes prior work on the same data.
	/// </summary>
	public struct ChunkActiveJob : IComponentData
	{
		public JobHandle Handle;
	}

	/// <summary>
	///     Per-chunk face-connectivity mask for graph-based occlusion culling, built at
	///     mesh time (GreedyMeshJob) and refreshed on every remesh. <see cref="Mask" /> bit
	///     <c>(from*8 + to)</c> is set when a sightline can pass through this chunk's
	///     non-opaque cells from face <c>from</c> to face <c>to</c>.
	///     Face indices: 0=X- 1=X+ 2=Y- 3=Y+ 4=Z- 5=Z+ (opposite = index ^ 1).
	///     A missing component means "assume fully transparent" (all faces connected) — the
	///     safe over-render default for not-yet-meshed / all-air chunks.
	/// </summary>
	public struct ChunkOcclusion : IComponentData
	{
		public ulong Mask;
	}
}