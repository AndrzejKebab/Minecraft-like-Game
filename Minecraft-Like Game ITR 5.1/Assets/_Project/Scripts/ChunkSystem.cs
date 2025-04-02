using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using PatataGames.JobScheduler;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PatataGames;

public class ChunkSystem : MonoBehaviour
{
    public MeshData MeshData;
    public static MeshDataNative MeshDataNative;
    public static Material Mat;
    private NativeParallelHashMap<int3, Chunk> chunkMap = new((int)math.pow(ChunkSize + 1, 3), Allocator.Persistent);
    private readonly List<int3> chunksToUpdate = new();
    private readonly Queue<int3> chunkProcessQueue = new();
    public static readonly byte ChunkSize = 32;
    private const byte viewdistance = 8;
    private const byte batchSize = 16;
    private bool isPopulating;
    private bool isCreatingMesh;
    private JobHandle createMeshJobHandle;
    private Mesh.MeshDataArray meshDataArray;
    private readonly Stopwatch sw = new();
    private JobForScheduler<PopulateVoxelMapParallel> populateJobScheduler;
    private List<int3> currentBatch = new();
    private int totalChunksProcessed;
    private int totalChunksToProcess;
    private HashSet<int3> processedChunks = new(); // Track which chunks have been processed

    public void Start()
    {
        MeshDataNative = new MeshDataNative(MeshData);
        sw.Start();
        
        // Queue chunks in order of distance from center for progressive loading
        QueueChunksProgressively();
        
        // Initialize job scheduler with appropriate capacity
        populateJobScheduler = new JobForScheduler<PopulateVoxelMapParallel>(64);
        
        // Start the progressive chunk loading process
        ProcessNextChunkBatch().Forget();
        
        Debug.Log($"Queued {totalChunksToProcess} chunks for processing");
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
        }

        totalChunksToProcess = chunkProcessQueue.Count;
        Debug.Log($"Total chunks to process: {totalChunksToProcess}");
    }

    private async UniTask ProcessNextChunkBatch()
    {
        try
        {
            int batchCount = 0;
            
            while (chunkProcessQueue.Count > 0)
            {
                batchCount++;
                
                // Take a small batch of chunks
                currentBatch.Clear();
                for (int i = 0; i < batchSize && chunkProcessQueue.Count > 0; i++)
                {
                    var chunk = chunkProcessQueue.Dequeue();
                    
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
                
                Debug.Log($"Processing batch {batchCount}: {chunksToUpdate.Count} chunks");
                
                // Populate chunks
                isPopulating = true;
                PopulateChunk();
                
                // Wait for population to complete
                await WaitForPopulationComplete();
                
                // Update progress
                totalChunksProcessed += currentBatch.Count;
                float progress = (float)totalChunksProcessed / totalChunksToProcess;
                Debug.Log($"Chunk processing progress: {progress:P2} ({totalChunksProcessed}/{totalChunksToProcess})");
                
                // Yield to prevent frame drops
                await UniTask.Yield();
            }
            
            Debug.Log($"All chunks processed in {sw.ElapsedMilliseconds}ms. Total chunks: {totalChunksProcessed}");
            sw.Stop();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in ProcessNextChunkBatch: {e.Message}\n{e.StackTrace}");
        }
    }

    private async UniTask WaitForPopulationComplete()
    {
        try
        {
            // Schedule all jobs and wait for them to complete
            await populateJobScheduler.ScheduleAll();
            await populateJobScheduler.Complete();
            isPopulating = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in population: {e.Message}\n{e.StackTrace}");
            isPopulating = false;
        }
    }

    private void PopulateChunk()
    {
        try
        {
            var job = new PopulateVoxelMapParallel()
            {
                ChunkMap = chunkMap.AsParallelWriter(),
                ChunksToUpdate = chunksToUpdate.ToNativeList(Allocator.TempJob)
            };
            populateJobScheduler.AddJob(job, chunksToUpdate.Count);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in PopulateChunk: {e.Message}\n{e.StackTrace}");
        }
    }
/* this dont work for now leave it
    private void CreateChunkMesh()
    {
        for (var i = 0; i < chunksToUpdate.Count; i++) 
            meshDataArray[i] = Mesh.AllocateWritableMeshData(1);
            
        createMeshJobHandle = new CreateMeshParallel
        {
            ChunkMap = chunkMap.AsReadOnly(),
            ChunksToUpdate = chunksToUpdate.ToNativeList(Allocator.TempJob),
            MeshDataArray = meshDataArray
        }.Schedule(chunksToUpdate.Count, default);
    }

    private void ApplyMeshData()
    {
        for (var i = 0; i < chunksToUpdate.Count; i++)
        {
            var chunkMesh = new Mesh();
            chunkMesh.MarkDynamic();
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray[i], chunkMesh);
            chunkMesh.RecalculateBounds();
            chunkMesh.RecalculateTangents();
            chunkMesh.Optimize();
            
            var chunk = new GameObject();
            chunk.AddComponent<MeshFilter>();
            var meshRenderer = chunk.AddComponent<MeshRenderer>();
            var chunkCollider = chunk.AddComponent<MeshCollider>();
            meshRenderer.material = Mat;
            chunkCollider.sharedMesh = chunkMesh;
            chunk.transform.SetParent(transform);
            chunk.transform.position = new Vector3(chunksToUpdate[i].x * ChunkSize, chunksToUpdate[i].y * ChunkSize,
                                                  chunksToUpdate[i].z * ChunkSize);
            chunk.name = $"Chunk [{chunksToUpdate[i].x}, {chunksToUpdate[i].y}, {chunksToUpdate[i].z}]";
        }
    }
    */
    private void OnDestroy()
    {
        // Clean up native collections
        if (chunkMap.IsCreated)
            chunkMap.Dispose();
            
        if (populateJobScheduler.JobHandlesCount > 0)
            populateJobScheduler.CompleteAll();
            
        populateJobScheduler.Dispose();
    }
}