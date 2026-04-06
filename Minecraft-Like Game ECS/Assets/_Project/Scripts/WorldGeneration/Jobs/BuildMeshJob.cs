using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Jobs
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct BuildMeshJob : IJob
	{
		private const MeshUpdateFlags UPDATE_FLAGS = MeshUpdateFlags.DontRecalculateBounds |
		                                             MeshUpdateFlags.DontValidateIndices |
		                                             MeshUpdateFlags.DontNotifyMeshUsers |
		                                             MeshUpdateFlags.DontResetBoneBounds |
		                                             MeshUpdateFlags.DontValidateLodRanges;

		[ReadOnly] public NativeArray<BlockState> Blocks;
		[ReadOnly] public NativeArray<Block>      BlockPrototypes;

		[NativeDisableContainerSafetyRestriction] [ReadOnly]
		public NativeArray<NativeVoxelMeshData> Meshes;

		[ReadOnly] public NativeArray<BlockState> NeighborZNeg;
		[ReadOnly] public NativeArray<BlockState> NeighborZPos;
		[ReadOnly] public NativeArray<BlockState> NeighborYNeg;
		[ReadOnly] public NativeArray<BlockState> NeighborYPos;
		[ReadOnly] public NativeArray<BlockState> NeighborXNeg;
		[ReadOnly] public NativeArray<BlockState> NeighborXPos;

		public            int                                    ChunkSize;
		[ReadOnly] public NativeArray<VertexAttributeDescriptor> Layout;
		[ReadOnly] public NativeArray<float3>                    FaceChecks;
		[ReadOnly] public NativeArray<float3>                    FaceTangents;

		public Mesh.MeshDataArray MeshDataArray;

		private static quaternion GetRotation(BlockDirectionType type, byte orientation)
		{
			return type switch
			       {
				       BlockDirectionType.None => quaternion.identity,
				       BlockDirectionType.YAxis => orientation switch
				                                   {
					                                   2 => quaternion.identity,                   // Face North
					                                   4 => quaternion.Euler(0, math.PI / 2f, 0),  // Face East
					                                   3 => quaternion.Euler(0, math.PI, 0),       // Face South
					                                   5 => quaternion.Euler(0, -math.PI / 2f, 0), // Face West
					                                   _ => quaternion.identity
				                                   },
				       BlockDirectionType.AllAxes => orientation switch
				                                     {
					                                     0 => quaternion.identity,                   // Top
					                                     1 => quaternion.Euler(math.PI, 0, 0),       // Bottom
					                                     2 => quaternion.Euler(math.PI / 2f, 0, 0),  // North
					                                     3 => quaternion.Euler(-math.PI / 2f, 0, 0), // South
					                                     4 => quaternion.Euler(0, 0, -math.PI / 2f), // East
					                                     5 => quaternion.Euler(0, 0, math.PI / 2f),  // West
					                                     _ => quaternion.identity
				                                     },
				       _ => quaternion.identity
			       };
		}

		public void Execute()
		{
			var solidMesh = new NativeMesh(Allocator.Temp);
			var fluidMesh = new NativeMesh(Allocator.Temp);

			if (Meshes.Length == 0) return;

			for (ushort x = 0; x < ChunkSize; x++)
			for (ushort y = 0; y < ChunkSize; y++)
			for (ushort z = 0; z < ChunkSize; z++)
			{
				var        index      = x | (y << 5) | (z << 10);
				BlockState blockState = Blocks[index];
				if (blockState.IsEmpty) continue;

				Block block = BlockPrototypes[blockState.ID];
				if (block.MeshID >= Meshes.Length) continue;

				NativeVoxelMeshData meshData = Meshes[block.MeshID];
				quaternion          rot      = GetRotation(block.DirectionType, blockState.Orientation);

				for (var i = 0; i < meshData.Triangles.Length; i++)
				{
					int4 quad = meshData.Triangles[i];

					float3 originalNormal  = FaceChecks[i];
					float3 originalTangent = FaceTangents[i];

					float3 rotatedNormal = math.round(math.mul(rot, originalNormal));
					var    dir           = new int3(rotatedNormal);

					if (NeighbourHidesFace(x, y, z, dir, block.IsTransparent))
						continue;

					var wPos    = new float3(x, y, z);
					var texBase = GetTextureIndex(originalNormal, block.BaseTextures);

					float3 v0 = math.mul(rot, meshData.Vertices[quad.x] - 0.5f) + 0.5f;
					float3 v1 = math.mul(rot, meshData.Vertices[quad.y] - 0.5f) + 0.5f;
					float3 v2 = math.mul(rot, meshData.Vertices[quad.z] - 0.5f) + 0.5f;
					float3 v3 = math.mul(rot, meshData.Vertices[quad.w] - 0.5f) + 0.5f;

					float3 rotatedTangent    = math.round(math.mul(rot, originalTangent));
					float3 originalBitangent = math.cross(originalNormal, originalTangent);
					float3 rotatedBitangent  = math.round(math.mul(rot, originalBitangent));
					float3 expectedBitangent = math.cross(rotatedNormal, rotatedTangent);
					var    tangentW          = math.dot(rotatedBitangent, expectedBitangent) >= 0f ? 1f : -1f;

					Vertex vert0 = CreateVertex(v0 + wPos, rotatedNormal, new float4(rotatedTangent, tangentW), 0, 0,
					                            texBase);
					Vertex vert1 = CreateVertex(v1 + wPos, rotatedNormal, new float4(rotatedTangent, tangentW), 0, 1,
					                            texBase);
					Vertex vert2 = CreateVertex(v2 + wPos, rotatedNormal, new float4(rotatedTangent, tangentW), 1, 0,
					                            texBase);
					Vertex vert3 = CreateVertex(v3 + wPos, rotatedNormal, new float4(rotatedTangent, tangentW), 1, 1,
					                            texBase);

					if (block.IsFluid) AddFace(vert0, vert1, vert2, vert3, ref fluidMesh);
					else AddFace(vert0, vert1, vert2, vert3, ref solidMesh);
				}
			}

			SetMeshDataArray(ref MeshDataArray, solidMesh, fluidMesh);
		}

		private bool NeighbourHidesFace(int x, int y, int z, int3 dir, bool isTransparent)
		{
			int    nx   = x + dir.x, ny = y + dir.y, nz = z + dir.z;
			ushort nbId = 0;

			if (nx < 0)
			{
				if (NeighborXNeg.IsCreated) nbId = NeighborXNeg[(ChunkSize - 1) | (ny << 5) | (nz << 10)].ID;
			}
			else if (nx >= ChunkSize)
			{
				if (NeighborXPos.IsCreated) nbId = NeighborXPos[0 | (ny << 5) | (nz << 10)].ID;
			}
			else if (ny < 0)
			{
				if (NeighborYNeg.IsCreated) nbId = NeighborYNeg[nx | ((ChunkSize - 1) << 5) | (nz << 10)].ID;
			}
			else if (ny >= ChunkSize)
			{
				if (NeighborYPos.IsCreated) nbId = NeighborYPos[nx | 0 | (nz << 10)].ID;
			}
			else if (nz < 0)
			{
				if (NeighborZNeg.IsCreated) nbId = NeighborZNeg[nx | (ny << 5) | ((ChunkSize - 1) << 10)].ID;
			}
			else if (nz >= ChunkSize)
			{
				if (NeighborZPos.IsCreated) nbId = NeighborZPos[nx | (ny << 5) | 0].ID;
			}
			else
			{
				nbId = Blocks[nx | (ny << 5) | (nz << 10)].ID;
			}

			if (nbId == 0) return false;
			return !(BlockPrototypes[nbId].IsTransparent && !isTransparent);
		}

		private static void AddFace(Vertex v0, Vertex v1, Vertex v2, Vertex v3, ref NativeMesh mesh)
		{
			var b = mesh.Vertices.Length;
			mesh.Vertices.Add(v0);
			mesh.Vertices.Add(v1);
			mesh.Vertices.Add(v2);
			mesh.Vertices.Add(v3);

			mesh.Triangles.Add(b);
			mesh.Triangles.Add(b + 1);
			mesh.Triangles.Add(b + 3);
			mesh.Triangles.Add(b);
			mesh.Triangles.Add(b + 3);
			mesh.Triangles.Add(b + 2);
		}

		private static Vertex CreateVertex(float3 pos, float3 norm, float4 tangent, float u, float v, float tBase)
		{
			return new Vertex
			       {
				       Position = new half4((half3)pos.xyz, (half)0),
				       Normal   = new half4((half3)norm.xyz, (half)0),
				       Tangent  = new half4(tangent),
				       UVs      = new half4((half)u, (half)v, (half)tBase, (half)0)
			       };
		}

		private static ushort GetTextureIndex(float3 normal, in NativeTexturesIDLayer layer)
		{
			return normal.y switch
			       {
				       > 0.5f  => layer.Top,
				       < -0.5f => layer.Bottom,
				       _ => normal.x switch
				            {
					            > 0.5f  => layer.Right,
					            < -0.5f => layer.Left,
					            _ => normal.z switch
					                 {
						                 > 0.5f  => layer.Front,
						                 < -0.5f => layer.Back,
						                 _       => layer.Front
					                 }
				            }
			       };
		}

		private void SetMeshDataArray(ref Mesh.MeshDataArray meshDataArray, NativeMesh solidMesh, NativeMesh fluidMesh)
		{
			Mesh.MeshData meshData = meshDataArray[0];

			var solidVertexCount = solidMesh.Vertices.Length;
			var fluidVertexCount = fluidMesh.Vertices.Length;
			var solidIndexCount  = solidMesh.Triangles.Length;
			var fluidIndexCount  = fluidMesh.Triangles.Length;
			var totalVertexCount = solidVertexCount + fluidVertexCount;

			if (totalVertexCount > 65535)
			{
				SetMeshDataArrayUInt32(ref meshDataArray, solidMesh, fluidMesh,
				                       solidVertexCount, fluidVertexCount,
				                       solidIndexCount, fluidIndexCount);
				return;
			}

			meshData.subMeshCount = 2;
			meshData.SetVertexBufferParams(totalVertexCount, Layout);
			meshData.SetIndexBufferParams(solidIndexCount + fluidIndexCount, IndexFormat.UInt16);

			NativeArray<Vertex> vertexData = meshData.GetVertexData<Vertex>();
			for (var i = 0; i < solidVertexCount; i++) vertexData[i] = solidMesh.Vertices[i];
			for (var i = 0; i < fluidVertexCount; i++) vertexData[solidVertexCount + i] = fluidMesh.Vertices[i];

			NativeArray<ushort> indexData                          = meshData.GetIndexData<ushort>();
			for (var i = 0; i < solidIndexCount; i++) indexData[i] = (ushort)solidMesh.Triangles[i];
			for (var i = 0; i < fluidIndexCount; i++)
				indexData[solidIndexCount + i] = (ushort)(fluidMesh.Triangles[i] + solidVertexCount);

			meshData.SetSubMesh(0, new SubMeshDescriptor(0, solidIndexCount)
			                       { firstVertex = 0, vertexCount = solidVertexCount, baseVertex = 0 }, UPDATE_FLAGS);
			meshData.SetSubMesh(1, new SubMeshDescriptor(solidIndexCount, fluidIndexCount)
			                       { firstVertex = solidVertexCount, vertexCount = fluidVertexCount, baseVertex = 0 },
			                    UPDATE_FLAGS);
		}

		private void SetMeshDataArrayUInt32(ref Mesh.MeshDataArray meshDataArray, NativeMesh solidMesh,
		                                    NativeMesh fluidMesh, int solidVertexCount, int fluidVertexCount,
		                                    int solidIndexCount, int fluidIndexCount)
		{
			Mesh.MeshData meshData = meshDataArray[0];
			meshData.subMeshCount = 2;
			meshData.SetVertexBufferParams(solidVertexCount + fluidVertexCount, Layout);
			meshData.SetIndexBufferParams(solidIndexCount + fluidIndexCount, IndexFormat.UInt32);

			NativeArray<Vertex> vertexData = meshData.GetVertexData<Vertex>();
			for (var i = 0; i < solidVertexCount; i++) vertexData[i] = solidMesh.Vertices[i];
			for (var i = 0; i < fluidVertexCount; i++) vertexData[solidVertexCount + i] = fluidMesh.Vertices[i];

			NativeArray<int> indexData                             = meshData.GetIndexData<int>();
			for (var i = 0; i < solidIndexCount; i++) indexData[i] = solidMesh.Triangles[i];
			for (var i = 0; i < fluidIndexCount; i++)
				indexData[solidIndexCount + i] = fluidMesh.Triangles[i] + solidVertexCount;

			meshData.SetSubMesh(0, new SubMeshDescriptor(0, solidIndexCount)
			                       { firstVertex = 0, vertexCount = solidVertexCount, baseVertex = 0 }, UPDATE_FLAGS);
			meshData.SetSubMesh(1, new SubMeshDescriptor(solidIndexCount, fluidIndexCount)
			                       { firstVertex = solidVertexCount, vertexCount = fluidVertexCount, baseVertex = 0 },
			                    UPDATE_FLAGS);
		}
	}
}