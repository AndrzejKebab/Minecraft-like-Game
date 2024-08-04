using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace PatataStudio
{
	public partial class ChunkSystem : SystemBase
	{
		private NativeList<int3> chunksToUpdate = new(Allocator.Persistent);
		public NativeParallelHashMap<int3, Chunk> ChunkMap = new((int)math.pow(9, 3),Allocator.Persistent);
		private FastNoise fastNoiseNodeTree;
		private IntPtr noiseNodeTreePtr;

		JobHandle voxelMapJobHandle;

		private bool isScheduled;
		private Stopwatch sw = new Stopwatch();

		protected override void OnCreate()
		{
			sw.Stop();
			base.OnCreate();
			SetupFastNoise();
			ScheduleChunks();
		}

		protected override void OnUpdate()
		{
			if (voxelMapJobHandle.IsCompleted && isScheduled)
			{
				voxelMapJobHandle.Complete();
				sw.Stop();
				Debug.Log("Job completed in " + sw.ElapsedMilliseconds + " ms");

				Debug.Log(ChunkMap[0].ToString());
				isScheduled = false;
			}
		}

		private void ScheduleChunks()
		{
			for (int x = -4; x <= 4; x++)
			{
				for (int y = -4; y <= 4; y++)
				{
					for (int z = -4; z <= 4; z++)
					{
						chunksToUpdate.Add(new int3(x, y, z));
					}
				}
			}

			voxelMapJobHandle = new VoxelMapParallel
			{
				NoiseNodeTreePtr = noiseNodeTreePtr,
				ChunksToUpdate = chunksToUpdate,
				ChunkMap = ChunkMap.AsParallelWriter()
			}.Schedule(chunksToUpdate.Length, 8);
			isScheduled = true;
			Debug.Log("Job created");
		}

		private void SetupFastNoise()
		{
			fastNoiseNodeTree = FastNoise.FromEncodedNodeTree("FwAAAIC/AACAPwAAAAAAAIA/GgABEQACAAAAAAAgQBAAAAAAQBMAw/UoPw0ABAAAAAAAIEApAABmZiY/AAAAAD8AzcxMPgAzMzM/AAAAAD8BBgA=");
			noiseNodeTreePtr = fastNoiseNodeTree.NodeHandlePtr;
		}
	}
}