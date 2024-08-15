using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using static PatataGames.GameSettings;

namespace PatataGames
{
	[UpdateInGroup(typeof(VoxelGameSystemGroup))]
	public partial class ChunkSystem : SystemBase
	{
		private NativeList<int3> chunksToUpdate = new(Allocator.Persistent);
		public NativeParallelHashMap<int3, Chunk> ChunkMap = new((int)math.pow((ViewDistance * 2) + 1, 3), Allocator.Persistent);

		private FastNoise continentalnessNodeTree;
		private FastNoise erosionNodeTree;
		private FastNoise peaksAndValleysNodeTree;
		private FastNoise cavesNodeTree;
		private IntPtr continentalnessNodePtr;
		private IntPtr erosionNodePtr;
		private IntPtr peaksAndValleysNodePtr;
		private IntPtr cavesNodePtr;

		private JobHandle voxelMapJobHandle;

		private bool isScheduled;
		private Stopwatch stopwatch = new Stopwatch();

		protected override void OnCreate()
		{
			stopwatch.Start();
			base.OnCreate();
			SetupFastNoise();
			ScheduleChunks();
		}

		protected override void OnUpdate()
		{
			if (voxelMapJobHandle.IsCompleted && isScheduled)
			{
				voxelMapJobHandle.Complete();
				stopwatch.Stop();
				Debug.Log("Job completed in " + stopwatch.ElapsedMilliseconds + " ms");

				Debug.Log(ChunkMap[0].ToString());
				isScheduled = false;
			}
		}

		private void ScheduleChunks()
		{
			for (int x = -ViewDistance; x <= ViewDistance; x++)
			{
				for (int y = -ViewDistance; y <= ViewDistance; y++)
				{
					for (int z = -ViewDistance; z <= ViewDistance; z++)
					{
						chunksToUpdate.Add(new int3(x, y, z));
					}
				}
			}

			voxelMapJobHandle = new VoxelMapParallel
			{
				NoiseNodeTreePtr = continentalnessNodePtr,
				ChunksToUpdate = chunksToUpdate,
				ChunkMap = ChunkMap.AsParallelWriter()
			}.Schedule(chunksToUpdate.Length, 8);
			isScheduled = true;
			Debug.Log("Job created");
		}

		private void SetupFastNoise()
		{
			continentalnessNodeTree = FastNoise.FromEncodedNodeTree(WorldManager.Instance.NoiseTypes[0].EncodedNoiseNodeTree);
			continentalnessNodePtr = continentalnessNodeTree.NodeHandlePtr;

			erosionNodeTree = FastNoise.FromEncodedNodeTree(WorldManager.Instance.NoiseTypes[1].EncodedNoiseNodeTree);
			erosionNodePtr = erosionNodeTree.NodeHandlePtr;

			peaksAndValleysNodeTree = FastNoise.FromEncodedNodeTree(WorldManager.Instance.NoiseTypes[2].EncodedNoiseNodeTree);
			peaksAndValleysNodePtr = peaksAndValleysNodeTree.NodeHandlePtr;

			cavesNodeTree = FastNoise.FromEncodedNodeTree(WorldManager.Instance.NoiseTypes[3].EncodedNoiseNodeTree);
			cavesNodePtr = cavesNodeTree.NodeHandlePtr;
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if(chunksToUpdate.IsCreated) chunksToUpdate.Dispose();
			if(ChunkMap.IsCreated) ChunkMap.Dispose();
		}
	}
}