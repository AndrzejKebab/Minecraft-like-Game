using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	public struct ChunkAccessor
	{
		[ReadOnly] public NativeHashMap<int3, Entity>     ChunkMap;
		[ReadOnly] public ComponentLookup<ChunkComponent> ChunkData;
        
		public int3 CenterChunkCoord;
		public int  ChunkSize;

		public ushort GetBlock(int3 localPos)
		{
			var chunkOffset = (int3)math.floor((float3)localPos / ChunkSize);
			int3 targetChunk = CenterChunkCoord + chunkOffset;

			if (chunkOffset.Equals(int3.zero))
			{
				// Local chunk read
				Entity entity = ChunkMap[targetChunk];
				return ChunkData[entity].BlockData[GetIndex(localPos)].ID;
			}
            
			// Neighbor chunk read
			if (!ChunkMap.TryGetValue(targetChunk, out Entity nEntity) || !ChunkData.HasComponent(nEntity))
				return 0; // Default to Air if unloaded
			NativeArray<BlockState> blockData = ChunkData[nEntity].BlockData;
			if (!blockData.IsCreated) return 0; // Air

			int3 nLocalPos = localPos - (chunkOffset * ChunkSize);
			return blockData[GetIndex(nLocalPos)].ID;

		}

		private int GetIndex(int3 pos) => pos.x + pos.y * ChunkSize + pos.z * ChunkSize * ChunkSize;
	}
}