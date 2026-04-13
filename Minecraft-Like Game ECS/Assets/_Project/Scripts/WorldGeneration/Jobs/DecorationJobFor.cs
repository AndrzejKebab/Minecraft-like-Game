using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Jobs
{
	[BurstCompile]
	public struct DecorationJobFor : IJobFor
	{
		public            int                                 ColorPhase;
		[ReadOnly] public NativeArray<Entity>                 Entities;
		[ReadOnly] public NativeArray<ChunkPositionComponent> Positions;
		[ReadOnly] public NativeHashMap<int3, Entity>         ChunkMap;
		[ReadOnly] public ComponentLookup<NeedsTerrainTag>    TerrainTagLookup;

		[NativeDisableContainerSafetyRestriction]
		public NativeHashMap<Entity, ChunkComponent> ChunkDataLookup;

		[ReadOnly] public NativeArray<OreSettings> OreTypes;
		public            int                      Seed,  ChunkSize;
		public            ushort                   AirID, GrassID, LogID, LeavesID;
		public            float                    TreeDensity;
		public            int                      MinTrunkHeight, MaxTrunkHeight;

		public NativeQueue<Entity>.ParallelWriter DirtiedNeighbors;
		public EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int index)
		{
			Entity entity = Entities[index];
			int3   pos    = Positions[index].ChunkCoord;

			var color = (pos.x & 1) * 2 + (pos.z & 1);
			if (color != ColorPhase) return;

			for (var dx = -1; dx <= 1; dx++)
			for (var dz = -1; dz <= 1; dz++)
			{
				if (dx == 0 && dz == 0) continue;
				if (!ChunkMap.TryGetValue(pos + new int3(dx, 0, dz), out Entity nEnt) ||
				    TerrainTagLookup.HasComponent(nEnt)) return;
			}

			if (!ChunkMap.TryGetValue(pos + new int3(0, 1, 0), out Entity topEnt) ||
			    TerrainTagLookup.HasComponent(topEnt)) return;

			var localMap = new NativeParallelHashMap<int3, ChunkBlockDataRef>(9, Allocator.Temp);
			for (var dx = -1; dx <= 1; dx++)
			for (var dz = -1; dz <= 1; dz++)
			{
				int3 nPos = pos + new int3(dx, 0, dz);
				if (ChunkMap.TryGetValue(nPos, out Entity nE))
					localMap.TryAdd(nPos, ChunkBlockDataRef.From(ChunkDataLookup[nE].BlockData));
			}

			if (ChunkMap.TryGetValue(pos + new int3(0, 1, 0), out Entity tE))
				localMap.TryAdd(pos + new int3(0, 1, 0), ChunkBlockDataRef.From(ChunkDataLookup[tE].BlockData));

			var dirtyMap = new NativeParallelHashMap<int3, bool>(9, Allocator.Temp);
			NativeParallelHashMap<int3, bool>.ParallelWriter dirtyWriter = dirtyMap.AsParallelWriter();
			int3 chunkWorldPos = pos * ChunkSize;
			NativeArray<BlockState> ownData = ChunkDataLookup[entity].BlockData;

			OreGenerator.Generate(ref localMap, ref dirtyWriter, ref OreTypes, ref chunkWorldPos, ChunkSize, Seed);
			TreeGenerator.Generate(ref ownData, ref localMap, ref dirtyWriter, ref chunkWorldPos, ChunkSize, Seed,
			                       TreeDensity, MinTrunkHeight, MaxTrunkHeight, AirID, GrassID, LogID, LeavesID);

			var hasBlocks = false;
			for (var i = 0; i < ownData.Length; i++)
				if (ownData[i].ID != 0)
				{
					hasBlocks = true;
					break;
				}

			ECB.RemoveComponent<NeedsDecorationTag>(index, entity);
			ECB.AddComponent<IsPopulated>(index, entity);

			if (hasBlocks || dirtyMap.ContainsKey(pos)) ECB.AddComponent<NeedsMeshSync>(index, entity);
			else ECB.AddComponent<IsEmpty>(index, entity);

			foreach (KeyValue<int3, bool> kvp in dirtyMap)
			{
				if (kvp.Key.Equals(pos)) continue;
				if (ChunkMap.TryGetValue(kvp.Key, out Entity dirtyNeighbor))
					DirtiedNeighbors.Enqueue(dirtyNeighbor);
			}

			localMap.Dispose();
			dirtyMap.Dispose();
		}
	}
}