using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PatataGames;

public class ChunkSystem : MonoBehaviour
{
	public                 MeshData                           MeshData;
	public static          MeshDataNative                     meshDataNative;
	public static          Material                           Mat;
	public                 NativeParallelHashMap<int3, Chunk> ChunkMap       = new((int)math.pow(ChunkSize + 1, 3), Allocator.Persistent);
	private                List<int3>                         chunksToUpdate = new ();
	public static readonly byte                               ChunkSize      = 32;
	private const          byte                               viewdistance   = 16;
	private                JobHandle                          populateJobHandle;
	private                bool                               isPopulating;
	private                JobHandle                          createMeshJobHandle;
	private                bool                               isCreatingMesh;
	private                NativeArray<Mesh.MeshDataArray>    meshDataArray;
	private readonly       Stopwatch                          sw = new();
	
	private JobForScheduler<PopulateVoxelMapParallel> populateJobScheduler = new ();
	
	public void Start()
	{
		meshDataNative = new MeshDataNative(MeshData);
		sw.Start();
		for (var x = -viewdistance; x <= viewdistance; x++)
		for (var z = -viewdistance; z <= viewdistance; z++)
		for (var y = -viewdistance; y <= viewdistance; y++)
			chunksToUpdate.Add(new int3(x, y, z));

		PopulateChunk();
	}

	private async void Update() => await ChunkJobsStateAsync();

	private async UniTask ChunkJobsStateAsync()
	{
		if (populateJobHandle.IsCompleted && isPopulating)
		{
			populateJobHandle.Complete();
			isPopulating = false;
			sw.Stop();
			Debug.Log($"Populate Time: {sw.ElapsedMilliseconds} ms");
			//CreateChunkMesh();
			//isCreatingMesh = true;
		}

		await UniTask.Yield();
		return;
		if (createMeshJobHandle.IsCompleted && isCreatingMesh)
		{
			createMeshJobHandle.Complete();
			isCreatingMesh = false;
		}

		if (createMeshJobHandle.IsCompleted) ApplyMeshData();
	}

	private void PopulateChunk()
	{
		populateJobHandle = new PopulateVoxelMapParallel
			{
				ChunkMap       = ChunkMap.AsParallelWriter(),
				ChunksToUpdate = chunksToUpdate.ToNativeList(Allocator.TempJob)
			}.Schedule(chunksToUpdate.Count, default);
		isPopulating = true;
	}

	private void CreateChunkMesh()
	{
		for (int i = 0; i < chunksToUpdate.Count; i++)
		{
			meshDataArray[i] = Mesh.AllocateWritableMeshData(1);
		}
		createMeshJobHandle = new CreateMeshParallel
		                      {
			                      ChunkMap       = ChunkMap.AsReadOnly(),
			                      ChunksToUpdate = chunksToUpdate.ToNativeList(Allocator.TempJob),
			                      MeshDataArray  = meshDataArray
		                      }.Schedule(chunksToUpdate.Count, default);
	}

	private void ApplyMeshData()
	{
		for (var i = 0; i < chunksToUpdate.Count; i++)
		{
			Mesh chunkMesh = new Mesh();
			chunkMesh.MarkDynamic();
			Mesh.ApplyAndDisposeWritableMeshData(meshDataArray[i], chunkMesh);
			chunkMesh.RecalculateBounds();
			chunkMesh.RecalculateTangents();
			chunkMesh.Optimize();
			GameObject chunk = new GameObject();
			chunk.AddComponent<MeshFilter>();
			var meshRenderer = chunk.AddComponent<MeshRenderer>();
			var chunkCollider = chunk.AddComponent<MeshCollider>();
			meshRenderer.material    = Mat;
			chunkCollider.sharedMesh = chunkMesh;
			chunk.transform.SetParent(transform);
			chunk.transform.position = new Vector3(chunksToUpdate[i].x * ChunkSize, chunksToUpdate[i].y * ChunkSize, chunksToUpdate[i].z * ChunkSize);
			chunk.name = $"Chunk [{chunksToUpdate[i].x}, {chunksToUpdate[i].y}, {chunksToUpdate[i].z}]";
		}
	}
}