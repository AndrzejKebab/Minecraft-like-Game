using PatataStudio.Global;
using PatataStudio.Global.Settings;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PatataStudio.World.Chunk
{
	public partial class ChunkSystem : SystemBase
	{
		private NativeParallelHashMap<int3, Chunk> chunkStorage;
		private ComputeShader chunkComputeShader;
		private int chunkSize;

		protected override void OnCreate()
		{
			base.OnCreate();
			chunkStorage = new NativeParallelHashMap<int3, Chunk>(GameSettings.ViewDistance * GameSettings.ViewDistance * GameSettings.ViewDistance, Allocator.Persistent);
			chunkSize = GameSettings.ChunkSize;

			// Load the compute shader
			chunkComputeShader = (ComputeShader)Resources.Load("ChunkMeshGenerator");
		}

		protected override void OnDestroy()
		{
			foreach (var chunk in chunkStorage.GetValueArray(Allocator.Temp))
			{
				chunk.Dispose();
			}
			chunkStorage.Dispose();
			base.OnDestroy();
		}

		protected override void OnUpdate()
		{
			NativeList<int3> chunksToUpdate = new NativeList<int3>(Allocator.TempJob);

			// Add chunks to update list here (example: iterate over view distance)
			for (int x = -GameSettings.ViewDistance / 2; x < GameSettings.ViewDistance / 2; x++)
			{
				for (int y = -GameSettings.ViewDistance / 2; y < GameSettings.ViewDistance / 2; y++)
				{
					for (int z = -GameSettings.ViewDistance / 2; z < GameSettings.ViewDistance / 2; z++)
					{
						int3 chunkPos = new int3(x, y, z);
						chunksToUpdate.Add(chunkPos);

						if (!chunkStorage.ContainsKey(chunkPos))
						{
							// Initialize new chunks
							Chunk newChunk = new Chunk(chunkSize, Allocator.Persistent);
							chunkStorage.TryAdd(chunkPos, newChunk);
						}
					}
				}
			}

			ChunkParallelJob chunkJob = new ChunkParallelJob
			{
				Accessor = new ChunkAccessor { ChunkMap = chunkStorage.AsReadOnly() },
				ChunksToUpdate = chunksToUpdate,
				ChunkMap = chunkStorage
			};

			JobHandle jobHandle = chunkJob.Schedule(chunksToUpdate.Length, 64);
			jobHandle.Complete();

			chunksToUpdate.Dispose();

			// Generate and render the chunk meshes
			GenerateAndRenderChunks();
		}

		private void GenerateAndRenderChunks()
		{
			foreach (var kvp in chunkStorage)
			{
				GenerateMesh(kvp.Key, kvp.Value);
			}
		}

		private void GenerateMesh(int3 chunkPos, Chunk chunk)
		{
			int vertexCount = chunkSize * chunkSize * chunkSize * 6 * 4; // 6 faces * 4 vertices
			int triangleCount = chunkSize * chunkSize * chunkSize * 6 * 6; // 6 faces * 6 indices

			ComputeBuffer vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 6);
			ComputeBuffer triangleBuffer = new ComputeBuffer(triangleCount, sizeof(int));
			ComputeBuffer voxelBuffer = new ComputeBuffer(chunk.VoxelMap.Length, sizeof(ushort));
			voxelBuffer.SetData(chunk.VoxelMap);

			GraphicsBuffer argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
			uint[] args = new uint[5] { (uint)triangleCount, 1, 0, 0, 0 };
			argsBuffer.SetData(args);

			chunkComputeShader.SetBuffer(0, "vertices", vertexBuffer);
			chunkComputeShader.SetBuffer(0, "triangles", triangleBuffer);
			chunkComputeShader.SetBuffer(0, "voxelData", voxelBuffer);
			chunkComputeShader.SetInt("chunkSize", chunkSize);
			chunkComputeShader.SetInts("chunkPosition", chunkPos.x, chunkPos.y, chunkPos.z);

			chunkComputeShader.Dispatch(0, chunkSize / 8, chunkSize / 8, chunkSize / 8);

			// Render the mesh using RenderPrimitivesIndirect
			Material material = new Material(Shader.Find("Standard"));
			material.SetBuffer("vertices", vertexBuffer);
			material.SetBuffer("triangles", triangleBuffer);

			RenderParams renderParams = new()
			{
				material = material,
				worldBounds = new Bounds(Vector3.one * chunkSize / 2, Vector3.one * chunkSize),
				receiveShadows = true,
				shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On
			};

			Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, argsBuffer);

			vertexBuffer.Dispose();
			triangleBuffer.Dispose();
			voxelBuffer.Dispose();
			argsBuffer.Dispose();
		}
	}
}
