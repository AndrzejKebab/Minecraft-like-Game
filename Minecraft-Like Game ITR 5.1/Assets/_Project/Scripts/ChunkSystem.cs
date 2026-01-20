using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using PatataGames.JobScheduler;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace PatataGames;

public class ChunkSystem : MonoBehaviour
{
	public MeshData       MeshData;
	public MeshDataNative MeshDataNative;
	public Material       Mat;

	// -- Native containers ------------------------------------------------------------------
	private NativeParallelHashMap<int3, Chunk> chunkMap;      // allocated in OnEnable
	private Mesh.MeshDataArray    meshDataArray; // allocated in OnEnable

	// -- Runtime lists / queues --------------------------------------------------------------
	private readonly List<int3>  chunksToUpdate    = [];
	private readonly Queue<int3> chunkProcessQueue = new();

	public const            byte CHUNK_SIZE   = 32;
	private static readonly byte viewdistance = 8;
	private const           byte batchSize    = 16;

	private readonly Stopwatch sw = new();

	private JobForScheduler<PopulateVoxelMapParallel> populateJobScheduler;
	private JobForScheduler<CreateMeshParallel>       createMeshJobScheduler;

	private readonly List<int3>    currentBatch = [];
	private          int           totalChunksProcessed;
	private          int           totalChunksToProcess;
	private readonly HashSet<int3> processedChunks = []; // Track which chunks have been processed

	private bool isPopulating;
	private bool isMeshing;

	private static NativeArray<VertexAttributeDescriptor> layout =
		new(2, Allocator.Persistent)
		{
			[0] = new VertexAttributeDescriptor(VertexAttribute.Position),
			[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
		};

	private void OnEnable()
	{
		// Allocate native collections only once the runtime is fully initialised
		var initialChunkCapacity = (int)math.pow(CHUNK_SIZE + 1, 3);
		chunkMap = new NativeParallelHashMap<int3, Chunk>(initialChunkCapacity, Allocator.Persistent);

		var meshCapacity = (int)math.pow(viewdistance * 2, 3);
		meshDataArray = new Mesh.MeshDataArray();
	}

	private void Start()
	{
		MeshDataNative = new MeshDataNative(MeshData);
		sw.Start();

		// Queue chunks in order of distance from center for progressive loading
		QueueChunksProgressively();

		// Initialise job schedulers
		populateJobScheduler                                  = new JobForScheduler<PopulateVoxelMapParallel>(64);
		//populateJobScheduler.baseScheduler.OnAllJobsCompleted = JobsComplete;
		//populateJobScheduler.baseScheduler.OnBatchCompleted   = BatchComplete;

		createMeshJobScheduler = new JobForScheduler<CreateMeshParallel>(64);

		// Start the progressive chunk loading process
		ProcessNextChunkBatch().Forget();

		Debug.Log($"Queued {totalChunksToProcess} chunks for processing");
	}

	private async void LateUpdate()
	{
		await populateJobScheduler.CompleteAsync();
		await createMeshJobScheduler.CompleteAsync();
	}

	private void QueueChunksProgressively()
	{
		chunkProcessQueue.Clear(); // Ensure queue is empty

		for (var x = -viewdistance; x <= viewdistance; x++)
		for (var z = -viewdistance; z <= viewdistance; z++)
		for (var y = -viewdistance; y <= viewdistance; y++)
		{
			var chunkPos = new int3(x, y, z);
			chunkProcessQueue.Enqueue(chunkPos);
			var chunk = new Chunk();
			chunkMap.Add(chunkPos, chunk);
		}

		totalChunksToProcess = chunkProcessQueue.Count;
		Debug.Log($"Total chunks to process: {totalChunksToProcess}");
	}

	private async UniTask ProcessNextChunkBatch()
	{
		try
		{
			while (chunkProcessQueue.Count > 0)
			{
				// Take a small batch of chunks
				currentBatch.Clear();
				for (var i = 0; i < batchSize && chunkProcessQueue.Count > 0; i++)
				{
					int3 chunk = chunkProcessQueue.Dequeue();

					// Skip if we've already processed this chunk
					if (processedChunks.Contains(chunk)) continue;

					currentBatch.Add(chunk);
					processedChunks.Add(chunk); // Mark as processed
				}

				// Skip empty batches
				if (currentBatch.Count == 0) continue;

				// Process this batch
				chunksToUpdate.Clear();
				chunksToUpdate.AddRange(currentBatch);

				// Populate chunks
				isPopulating = true;
				PopulateChunk();

				// Wait for population to complete
				await WaitForPopulationComplete();
				
				if (!isPopulating)
				{
					isMeshing = true;
					CreateChunkMesh();
				}

				await WaitForMeshGenerationComplete();

				ApplyMeshData();
				
				// Update progress
				totalChunksProcessed += currentBatch.Count;
				var progress = (float)totalChunksProcessed / totalChunksToProcess;
				Debug.Log($"Chunk processing progress: {progress:P2} ({totalChunksProcessed}/{totalChunksToProcess})");

				// Yield to prevent frame drops
				await UniTask.Yield();
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"Error in ProcessNextChunkBatch: {e.Message}\n{e.StackTrace}");
		}
	}

	private static void BatchComplete(int remainingChunks)
	{
		Debug.Log("Chunks left to process: " + remainingChunks);
	}

	private void JobsComplete()
	{
		Debug.Log($"All chunks processed in {sw.ElapsedMilliseconds}ms. Total chunks: {totalChunksProcessed}");
		sw.Stop();
	}

	private async UniTask WaitForPopulationComplete()
	{
		// Schedule all jobs and wait for them to complete
		await populateJobScheduler.ScheduleJobsAsync();
		isPopulating = false;
	}

	private async UniTask WaitForMeshGenerationComplete()
	{
		// Schedule all jobs and wait for them to complete
		await createMeshJobScheduler.ScheduleJobsAsync();
		isMeshing = false;
	}

	private void PopulateChunk()
	{
		var job = new PopulateVoxelMapParallel
		          {
			          ChunkMap       = chunkMap.AsParallelWriter(),
			          ChunkMapRW     = chunkMap.AsReadOnly(),
			          ChunksToUpdate = chunksToUpdate.ToNativeList(Allocator.Persistent)
		          };
		populateJobScheduler.AddJob(job, chunksToUpdate.Count);
	}

	private void CreateChunkMesh()
	{
		meshDataArray = Mesh.AllocateWritableMeshData(chunksToUpdate.Count);

		var job = new CreateMeshParallel
		          {
			          ChunkMap       = chunkMap.AsReadOnly(),
			          ChunksToUpdate = chunksToUpdate.ToNativeList(Allocator.TempJob),
			          MeshLibrary = new NativeArray<MeshDataNative>(1, Allocator.TempJob)
			                        {
				                        [0] = MeshDataNative
			                        },
			          Layout        = layout,
			          MeshDataArray = meshDataArray
		          };
		createMeshJobScheduler.AddJob(job, chunksToUpdate.Count);
	}

	private void ApplyMeshData()
	{
		var meshList = new List<Mesh>();
		for (var i = 0; i < chunksToUpdate.Count; i++)
		{
			var mesh = new Mesh();
			mesh.MarkDynamic();
			meshList.Add(mesh);
		}
		
		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshList);
		
		for (var i = 0; i < chunksToUpdate.Count; i++)
		{
			meshList[i].RecalculateBounds();
			meshList[i].RecalculateTangents();
			meshList[i].Optimize();

			var chunk = new GameObject();
			chunk.AddComponent<MeshFilter>();
			var meshRenderer  = chunk.AddComponent<MeshRenderer>();
			var chunkCollider = chunk.AddComponent<MeshCollider>();
			meshRenderer.material    = Mat;
			chunkCollider.sharedMesh = meshList[i];
			chunk.transform.SetParent(transform);
			chunk.transform.position = new Vector3(chunksToUpdate[i].x * CHUNK_SIZE, chunksToUpdate[i].y * CHUNK_SIZE,
			                                       chunksToUpdate[i].z * CHUNK_SIZE);
			chunk.name = $"Chunk [{chunksToUpdate[i].x}, {chunksToUpdate[i].y}, {chunksToUpdate[i].z}]";
		}
	}

	private void OnDestroy()
	{
		// Clean up native collections
		if (chunkMap.IsCreated) chunkMap.Dispose();
		if (layout.IsCreated) layout.Dispose();

		if (populateJobScheduler.JobsCount > 0)
			populateJobScheduler.CompleteImmediate();

		populateJobScheduler.Dispose();
		createMeshJobScheduler.Dispose();
	}
}