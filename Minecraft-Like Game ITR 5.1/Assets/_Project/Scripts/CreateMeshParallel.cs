using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Unity.Runtime;
using static PatataGames.ChunkSystem;

namespace PatataGames;

/// <summary>
///     Builds a mesh for every chunk listed in <see cref="ChunksToUpdate" />.
///     Each voxel stores a byte/block-id –&gt; 0 = air, 1…N = solid type.
///     The type maps to <see cref="MeshLibrary" /> which is a NativeArray&lt;MeshDataNative&gt;
///     converted once on the main-thread from MeshData ScriptableObjects.
/// </summary>
[BurstCompile(OptimizeFor = OptimizeFor.Performance,
	             FloatMode = FloatMode.Fast,
	             FloatPrecision = FloatPrecision.Low)]
public struct CreateMeshParallel : IJobFor
{
	// ──────────────────────────────────────────────────────────────────────────
	// INPUT (READ-ONLY)
	// ──────────────────────────────────────────────────────────────────────────
	[NativeDisableContainerSafetyRestriction]
	[ReadOnly] public          NativeParallelHashMap<int3, Chunk>.ReadOnly ChunkMap;
	[ReadOnly] public          NativeList<int3>                            ChunksToUpdate;
	[NativeDisableContainerSafetyRestriction]
	[ReadOnly] public          NativeArray<MeshDataNative>                 MeshLibrary;
	[ReadOnly] public NativeArray<VertexAttributeDescriptor> Layout;

	// ──────────────────────────────────────────────────────────────────────────
	// OUTPUT ‑ one MeshData per chunk
	// ──────────────────────────────────────────────────────────────────────────
	[NativeDisableContainerSafetyRestriction]
	public Mesh.MeshDataArray MeshDataArray;


	// ──────────────────────────────────────────────────────────────────────────
	// EXECUTION
	// ──────────────────────────────────────────────────────────────────────────
	public void Execute(int index)
	{
		int3 chunkPos = ChunksToUpdate[index];

		var verts     = new NativeList<float3>(512, Allocator.Temp);
		var uvs       = new NativeList<float2>(512, Allocator.Temp);
		var triangles = new NativeList<int>(1024, Allocator.Temp);

		GenerateChunkMesh(chunkPos, ref verts, ref uvs, ref triangles);
		WriteToMeshData(index, verts, uvs, triangles);

		verts.Dispose();
		uvs.Dispose();
		triangles.Dispose();
	}

	// ──────────────────────────────────────────────────────────────────────────
	// CHUNK LOOP
	// ──────────────────────────────────────────────────────────────────────────
	private void GenerateChunkMesh(int3                   chunkPos,
	                               ref NativeList<float3> verts,
	                               ref NativeList<float2> uvs,
	                               ref NativeList<int>    triangles)
	{
		Chunk     chunk = ChunkMap[chunkPos];

		for (var y = 0; y < CHUNK_SIZE; y++)
		for (var z = 0; z < CHUNK_SIZE; z++)
		for (var x = 0; x < CHUNK_SIZE; x++)
		{
			var blockId = chunk.VoxelMap.GetAtFlatIndex(CHUNK_SIZE, x, y, z);
			if (blockId == 0) continue; // air

			MeshDataNative meshType = MeshLibrary[0];
			AddVoxel(new int3(x, y, z),
			         chunkPos,
			         meshType,
			         ref verts, ref uvs, ref triangles);
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	// V O X E L   M E S H I N G
	// ──────────────────────────────────────────────────────────────────────────
	private void AddVoxel(int3                   voxelLocal,
	                      int3                   chunkPos,
	                      MeshDataNative         meshType,
	                      ref NativeList<float3> verts,
	                      ref NativeList<float2> uvs,
	                      ref NativeList<int>    triangles)
	{
		// iterate every authored face in the mesh type
		for (var f = 0; f < meshType.FaceDatas.Length; f++)
		{
			FaceDataNative face = meshType.FaceDatas[f];

			// neighbour direction = rounded normal (assumes axis-aligned faces)
			var dir = new int3(
			                   (int)math.round(face.Normal.x),
			                   (int)math.round(face.Normal.y),
			                   (int)math.round(face.Normal.z));

			int3 neighbourLocal = voxelLocal + dir;
			if (NeighbourIsSolid(chunkPos, neighbourLocal)) continue; // culled

			var baseIdx = verts.Length;

			// vertices / uvs ---------------------------------------------------
			for (var v = 0; v < face.Vertices.Length; v++)
			{
				Vertex src = face.Vertices[v];
				verts.Add(voxelLocal + src.Position); // position offset by voxel
				uvs.Add(src.UV);
			}

			// indices ----------------------------------------------------------
			var vc = face.Vertices.Length;
			switch (vc)
			{
				case 4:
					// 0-1-2, 0-2-3
					triangles.Add(baseIdx + 0);
					triangles.Add(baseIdx + 1);
					triangles.Add(baseIdx + 2);

					triangles.Add(baseIdx + 0);
					triangles.Add(baseIdx + 2);
					triangles.Add(baseIdx + 3);
					break;
				case 3:
					triangles.Add(baseIdx + 0);
					triangles.Add(baseIdx + 1);
					triangles.Add(baseIdx + 2);
					break;
				default:
				{
					// N-gon fan fallback (assumes convex)
					for (var t = 2; t < vc; t++)
					{
						triangles.Add(baseIdx + 0);
						triangles.Add(baseIdx + t - 1);
						triangles.Add(baseIdx + t);
					}

					break;
				}
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	// S O L I D   C H E C K  (cross-chunk aware)
	// ──────────────────────────────────────────────────────────────────────────
	private bool NeighbourIsSolid(int3 baseChunkPos, int3 localPos)
	{
		 int3 worldVoxel = baseChunkPos * CHUNK_SIZE + localPos;
        
                // Optimized Math for Chunk Size 32 (2^5):
                // ---------------------------------------
                // Right shift by 5 is equivalent to floor(val / 32.0).
                // This correctly handles negative numbers (e.g., -1 >> 5 == -1),
                // whereas integer division would truncate (-1 / 32 == 0).
                int3 chkPos = worldVoxel >> 5;
        
                // Bitwise AND with 31 is equivalent to modulo 32.
                // This handles wrapping correctly (e.g., -1 & 31 == 31).
                int3 local = worldVoxel & (CHUNK_SIZE - 1);
        
                if (!ChunkMap.ContainsKey(chkPos)) return true;
                Chunk chunk = ChunkMap[chkPos];
                
                return chunk.VoxelMap.GetAtFlatIndex(CHUNK_SIZE, local.x, local.y, local.z) != 0;
	}

	// ──────────────────────────────────────────────────────────────────────────
	// L O W  ‑  L E V E L   M E S H   O U T P U T
	// ──────────────────────────────────────────────────────────────────────────
	private void WriteToMeshData(int                meshDataIndex,
	                             NativeList<float3> verts,
	                             NativeList<float2> uvs,
	                             NativeList<int>    triangles)
	{
		// 1. Get the handle
		Mesh.MeshData meshData = MeshDataArray[meshDataIndex];
        
		// 2. EXPLICITLY set the submesh count. 
		// Without this, SetSubMesh(0, ...) throws "should be [0,0)"
		meshData.subMeshCount = 1;

		// 3. Set buffer params
		meshData.SetVertexBufferParams(verts.Length, Layout);
		meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);

		// 4. Write Data
		NativeArray<Vertex> vtx = meshData.GetVertexData<Vertex>();
		for (var i = 0; i < verts.Length; i++)
			vtx[i] = new Vertex { Position = verts[i], UV = uvs[i] };

		NativeArray<int> indexData = meshData.GetIndexData<int>();
		indexData.CopyFrom(triangles.AsArray());

		// 5. Define SubMesh
		meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));
	}
}