using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using static PatataGames.GameSettings;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mesh;

namespace PatataGames
{	
	public class ChunkSystem : MonoBehaviour
	{
		private NativeList<int3> chunksToUpdate = new();
		public NativeParallelHashMap<int3, Chunk> ChunkStorage = new((int)math.pow((ViewDistance * 2) + 1, 3), Allocator.Persistent);
		private NativeList<int3> activeChunks = new();
		private NativeList<Voxel> voxelList = new();

		private FastNoise continentalnessNodeTree;
		private FastNoise erosionNodeTree;
		private FastNoise peaksAndValleysNodeTree;
		private FastNoise cavesNodeTree;
		private IntPtr continentalnessNodePtr;
		private IntPtr erosionNodePtr;
		private IntPtr peaksAndValleysNodePtr;
		private IntPtr cavesNodePtr;

		private JobHandle voxelMapJobHandle;
		private JobHandle meshChunkJobHandle;

		private bool isScheduled;
		private Stopwatch stopwatch = new Stopwatch();
		private int3 PlayerChunkCoord;
		private int3 playerLastChunkCoord;
		private Transform PlayerTransform;

		public void Start()
		{
			stopwatch.Start();
			PlayerTransform = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
			SetupFastNoise();
			CheckViewDistance();
		}

		public void Update()
		{
			PlayerChunkCoord = Utils.GetChunkCoordFromGlobalPosition(new float3(PlayerTransform.position));
			if (!PlayerChunkCoord.Equals(playerLastChunkCoord))
			{
				CheckViewDistance();
			}

			if (voxelMapJobHandle.IsCompleted && isScheduled)
			{
				voxelMapJobHandle.Complete();
				stopwatch.Stop();
				Debug.Log("Job completed in " + stopwatch.ElapsedMilliseconds + " ms");
				isScheduled = false;
			}
		}

		private void CheckViewDistance()
		{
			var coord = Utils.GetChunkCoordFromGlobalPosition(PlayerTransform.position);
			List<int3> previouslyActiveChunks = new(activeChunks);
			activeChunks.Clear();

			playerLastChunkCoord = PlayerChunkCoord;

			for (var y = coord.y - ViewDistance; y < coord.y + ViewDistance; y++)
			{
				for (var x = coord.x - ViewDistance; x < coord.x + ViewDistance; x++)
				{
					for (var z = coord.z - ViewDistance; z < coord.z + ViewDistance; z++)
					{
						if (!ChunkStorage.ContainsKey(new int3(x, y, z)))
						{
							chunksToUpdate.Add(new int3(x, y, z));
						}
						activeChunks.Add(new int3(x, y, z));
						previouslyActiveChunks.Remove(new int3(x, y, z));
					}
				}
			}

			foreach (var chunk in previouslyActiveChunks)
			{
				ChunkStorage.Remove(new int3(chunk.x, chunk.y, chunk.z));
			}
			previouslyActiveChunks.Clear();

			SheduleChunks();
		}

		private void SheduleChunks()
		{
			voxelMapJobHandle = new VoxelMapParallel
			{
				NoiseNodeTreePtr = continentalnessNodePtr,
				ChunksToUpdate = chunksToUpdate,
				ChunkMap = ChunkStorage.AsParallelWriter()
			}.Schedule(chunksToUpdate.Length, 8);
			isScheduled = true;
			Debug.Log("Job created");
		}

		private void MeshChunks()
		{
			meshChunkJobHandle = new MeshChunkParallel
			{
				ChunksToUpdate = chunksToUpdate,
				ChunkStorage = ChunkStorage.AsReadOnly(),
				VoxelList = voxelList,
				MeshDataArrays = AllocateWritableMeshData(activeChunks.Length),
			}.Schedule(activeChunks.Length, 8);
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

		public void OnDestroy()
		{
			if(ChunkStorage.IsCreated) ChunkStorage.Dispose();
		}
	}
}