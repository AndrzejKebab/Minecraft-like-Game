using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime;
using UtilityLibrary.Unity.Runtime.PriorityQueue;
using static PatataStudio.GameSettings;

namespace PatataStudio.World
{
	public class ChunkPool
	{

		private ObjectPool<ChunkBehaviour> Pool;
		private Dictionary<int3, ChunkBehaviour> MeshMap;
		private HashSet<int3> ColliderSet;
		private SimpleFastPriorityQueue<int3, int> Queue;

		private int3 Focus;
		private int ChunkPoolSize;

		internal ChunkPool(Transform transform)
		{
			ChunkPoolSize = (int)MathF.Pow((ViewDistance + 2), 3);

			MeshMap = new Dictionary<int3, ChunkBehaviour>(ChunkPoolSize);
			ColliderSet = new HashSet<int3>((int)MathF.Pow((ViewDistance + 2), 3));
			Queue = new SimpleFastPriorityQueue<int3, int>();

			Pool = new ObjectPool<ChunkBehaviour>( // pool size = x^2 + 1
				() => {
					var go = new GameObject("Chunk", typeof(ChunkBehaviour))
					{
						transform = {
							parent = transform
						},
					};

					var collider = new GameObject("Collider", typeof(MeshCollider))
					{
						transform = {
							parent = go.transform
						},
						tag = "Chunk"
					};

					go.SetActive(false);

					var chunkBehaviour = go.GetComponent<ChunkBehaviour>();

					chunkBehaviour.Init(collider.GetComponent<MeshCollider>());

					return chunkBehaviour;
				},
				chunkBehaviour => chunkBehaviour.gameObject.SetActive(true),
				chunkBehaviour => chunkBehaviour.gameObject.SetActive(false),
				null, false, ChunkPoolSize, ChunkPoolSize
			);
		}

		internal bool IsActive(int3 pos) => MeshMap.ContainsKey(pos);
		internal bool IsCollidable(int3 pos) => ColliderSet.Contains(pos);

		internal void FocusUpdate(int3 focus)
		{
			Focus = focus;

			foreach (var position in Queue)
			{
				Queue.UpdatePriority(position, -(position - Focus).SqrMagnitude());
			}
		}

		internal ChunkBehaviour Claim(int3 position)
		{
			if (MeshMap.ContainsKey(position))
			{
				throw new InvalidOperationException($"Chunk ({position}) already active");
			}

			// Reclaim
			if (Queue.Count >= ChunkPoolSize)
			{
				var reclaim = Queue.Dequeue();
				var reclaim_behaviour = MeshMap[reclaim];

				reclaim_behaviour.Collider.sharedMesh = null;

				Pool.Release(reclaim_behaviour);
				MeshMap.Remove(reclaim);
				ColliderSet.Remove(reclaim);
			}

			// Claim
			var behaviour = Pool.Get();

			behaviour.transform.position = math.float3(position);
			behaviour.name = $"Chunk({position})";

			MeshMap.Add(position, behaviour);
			Queue.Enqueue(position, -(position - Focus).SqrMagnitude());

			return behaviour;
		}

		internal Dictionary<int3, ChunkBehaviour> GetActiveMeshes(List<int3> positions)
		{
			var map = new Dictionary<int3, ChunkBehaviour>();

			for (int i = 0; i < positions.Count; i++)
			{
				var position = positions[i];

				if (IsActive(position)) map.Add(position, MeshMap[position]);
			}

			return map;
		}

		internal void ColliderBaked(int3 position)
		{
			ColliderSet.Add(position);
		}

		internal ChunkBehaviour Get(int3 position)
		{
			if (!MeshMap.ContainsKey(position))
			{
				throw new InvalidOperationException($"Chunk ({position}) isn't active");
			}

			return MeshMap[position];
		}

	}
}