using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static PatataGames.GameSettings;

namespace PatataGames
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public struct MeshChunkParallel : IJobParallelFor
	{
		[ReadOnly] public NativeList<int3> ChunksToUpdate;
		[ReadOnly] public NativeParallelHashMap<int3, Chunk>.ReadOnly ChunkStorage;
		[ReadOnly] public NativeList<Voxel> VoxelList;
		[WriteOnly] public Mesh.MeshDataArray MeshDataArrays;

		public void Execute(int index)
		{
			var chunk = ChunkStorage[ChunksToUpdate[index]];
			CreateMesh(chunk);
		}

		private void CreateMesh(Chunk chunk)
		{
			int index = 0;
			var indices = new NativeList<int>(Allocator.Temp);
			var vertices = new NativeList<Vertex>(Allocator.Temp);
			var normals = new NativeList<int3>(Allocator.Temp);

			for (int i = 0; i < chunk.VoxelMap.Length; i++)
			{
				var currentVoxelID = chunk.VoxelMap.Get(i);
				var currentVoxel = VoxelList[currentVoxelID];

				for (int f = 0; f < 6; f++)
				{
					var faceData = currentVoxel.FaceDatas[f];

					if (IsVoxelVisible(i.IntToInt3(), chunk, new int3(faceData.Normal))) continue;

					var verticesPerFace = faceData.Vertices.Length;

					// Add vertices, UVs, and normals.
					for (int j = 0; j < verticesPerFace; j++)
					{
						vertices.Add(faceData.Vertices[j]);
						normals.Add(faceData.Normal);
					}

					if (verticesPerFace % 4 == 0)
					{
						// Calculate the number of groups of 4 vertices.
						int groups = verticesPerFace / 4;

						// Add triangles for each group of 4 vertices.
						for (int l = 0; l < groups; l++)
						{
							int start = l * 4;

							// First triangle (0, 1, 2).
							indices.Add(index + start + 0);
							indices.Add(index + start + 1);
							indices.Add(index + start + 2);
							// Second triangle (2, 1, 3).
							indices.Add(index + start + 2);
							indices.Add(index + start + 1);
							indices.Add(index + start + 3);
						}
					}
					else
					{
						for (int l = 0; l < verticesPerFace / 3; l++)
						{
							indices.Add(index + l * 3);
							indices.Add(index + l * 3 + 1);
							indices.Add(index + l * 3 + 2);
						}
					}

					index += verticesPerFace;
				}
			}
			var decs = new VertexAttributeDescriptor[]
			{
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
			};

			MeshDataArrays[index].SetVertexBufferParams(vertices.Length, decs);
			MeshDataArrays[index].SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
			MeshDataArrays[index].SetSubMesh(0, new SubMeshDescriptor(0, indices.Length), MeshUpdateFlags.DontRecalculateBounds);
		}

		private bool IsVoxelVisible(int3 voxelPos, Chunk chunk, int3 normal)
		{
			if (IsVoxelInChunk(voxelPos))
			{
				var index = voxelPos.x + voxelPos.y * ChunkSize + voxelPos.z * ChunkSize * ChunkSize;
				var voxel = chunk.VoxelMap.Get(index);
				return VoxelList[voxel].IsSolid;
			}
			else
			{
				var chunkCoord = Utils.GetChunkCoordFromGlobalPosition(voxelPos);

				if (ChunkStorage.TryGetValue(chunkCoord, out var neighborChunk))
				{
					int index = voxelPos.Int3ToInt();
					var voxel = neighborChunk.VoxelMap.Get(index);
					return VoxelList[voxel].IsSolid;
				}
				else
				{
					var globalPosition = Utils.VoxelPositionToGlobalPosition(voxelPos + normal, chunkCoord);
					var neighborChunkCoord = Utils.GetChunkCoordFromGlobalPosition(globalPosition);
					if (ChunkStorage.TryGetValue(neighborChunkCoord, out neighborChunk))
					{
						var neighborVoxelPos = voxelPos + normal;
						int index = neighborVoxelPos.Int3ToInt();
						var voxel = neighborChunk.VoxelMap.Get(index);
						return VoxelList[voxel].IsSolid;
					}
					return true; // returning true so face wont be rendered
				}

			}
		}

		private bool IsVoxelInChunk(int3 voxelPos)
		{
			return voxelPos.x >= 0 && voxelPos.x < ChunkSize &&
				voxelPos.y >= 0 && voxelPos.y < ChunkSize &&
				voxelPos.z >= 0 && voxelPos.z < ChunkSize;
		}
	}
}