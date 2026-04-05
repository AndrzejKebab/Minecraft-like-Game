using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration
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

		[ReadOnly] public NativeArray<ushort> Blocks;
		[ReadOnly] public NativeArray<Block>  BlockPrototypes;

		[NativeDisableContainerSafetyRestriction] [ReadOnly]
		public NativeArray<NativeVoxelMeshData> Meshes;

		[ReadOnly] public NativeArray<ushort> NeighborZNeg;
		[ReadOnly] public NativeArray<ushort> NeighborZPos;
		[ReadOnly] public NativeArray<ushort> NeighborYNeg;
		[ReadOnly] public NativeArray<ushort> NeighborYPos;
		[ReadOnly] public NativeArray<ushort> NeighborXNeg;
		[ReadOnly] public NativeArray<ushort> NeighborXPos;

		public            int                                    ChunkSize;
		[ReadOnly] public NativeArray<VertexAttributeDescriptor> Layout;
		[ReadOnly] public NativeArray<float3>                    FaceChecks;
		[ReadOnly] public NativeArray<float3>                    FaceTangents;

		public Mesh.MeshDataArray MeshDataArray;

		public void Execute()
		{
			var solidMesh = new NativeMesh(Allocator.Temp);
			var fluidMesh = new NativeMesh(Allocator.Temp);

			if (Meshes.Length == 0) return;

			for (ushort x = 0; x < ChunkSize; x++)
			for (ushort y = 0; y < ChunkSize; y++)
			for (ushort z = 0; z < ChunkSize; z++)
			{
				var index   = x | (y << 5) | (z << 10);
				var blockId = Blocks[index];
				if (blockId == 0) continue;

				Block block = BlockPrototypes[blockId];
				if (block.MeshID >= Meshes.Length) continue;

				NativeVoxelMeshData meshData = Meshes[block.MeshID];

				for (var i = 0; i < meshData.Triangles.Length; i++)
				{
					int4   quad = meshData.Triangles[i];
					float3 v0   = meshData.Vertices[quad.x];
					float3 v1   = meshData.Vertices[quad.y];
					float3 v2   = meshData.Vertices[quad.z];
					float3 v3   = meshData.Vertices[quad.w];


					float3 normal  = FaceChecks[i];
					int3   dir     = new(normal);
					float4 tangent = new(FaceTangents[i], 1);

					if (NeighbourHidesFace(x, y, z, dir, block.IsTransparent))
						continue;

					var wPos    = new float3(x, y, z);
					var texBase = GetTextureIndex(normal, block.BaseTextures);

					Vertex vert0 = CreateVertex(v0 + wPos, normal, tangent, 0, 0, texBase);
					Vertex vert1 = CreateVertex(v1 + wPos, normal, tangent, 0, 1, texBase);
					Vertex vert2 = CreateVertex(v2 + wPos, normal, tangent, 1, 0, texBase);
					Vertex vert3 = CreateVertex(v3 + wPos, normal, tangent, 1, 1, texBase);

					if (block.IsFluid) AddFace(vert0, vert1, vert2, vert3, ref fluidMesh);
					else AddFace(vert0, vert1, vert2, vert3, ref solidMesh);
				}
			}

			SetMeshDataArray(ref MeshDataArray, solidMesh, fluidMesh);
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
				       UVs      = new half4((half)u, (half)v, (half)tBase, (half)0),
			       };
		}

		private bool NeighbourHidesFace(int x, int y, int z, int3 dir, bool isTransparent)
		{
			int    nx   = x + dir.x, ny = y + dir.y, nz = z + dir.z;
			ushort nbId = 0;

			if (nx < 0)
			{
				if (NeighborXNeg.IsCreated) nbId = NeighborXNeg[(ChunkSize - 1) | (ny << 5) | (nz << 10)];
			}
			else if (nx >= ChunkSize)
			{
				if (NeighborXPos.IsCreated) nbId = NeighborXPos[0 | (ny << 5) | (nz << 10)];
			}
			else if (ny < 0)
			{
				if (NeighborYNeg.IsCreated) nbId = NeighborYNeg[nx | ((ChunkSize - 1) << 5) | (nz << 10)];
			}
			else if (ny >= ChunkSize)
			{
				if (NeighborYPos.IsCreated) nbId = NeighborYPos[nx | 0 | (nz << 10)];
			}
			else if (nz < 0)
			{
				if (NeighborZNeg.IsCreated) nbId = NeighborZNeg[nx | (ny << 5) | ((ChunkSize - 1) << 10)];
			}
			else if (nz >= ChunkSize)
			{
				if (NeighborZPos.IsCreated) nbId = NeighborZPos[nx | (ny << 5) | 0];
			}
			else
			{
				nbId = Blocks[nx | (ny << 5) | (nz << 10)];
			}

			if (nbId == 0) return false;
			return !(BlockPrototypes[nbId].IsTransparent && !isTransparent);
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

			// Safety: UInt16 max is 65535 — log if exceeded rather than silently corrupt
			// This should never fire for normal terrain with CHUNK_SIZE=32
			if (totalVertexCount > 65535)
			{
				// Fall back to UInt32 for this chunk only
				SetMeshDataArrayUInt32(ref meshDataArray, solidMesh, fluidMesh,
				                       solidVertexCount, fluidVertexCount,
				                       solidIndexCount, fluidIndexCount);
				return;
			}

			meshData.subMeshCount = 2;
			meshData.SetVertexBufferParams(totalVertexCount, Layout);
			meshData.SetIndexBufferParams(solidIndexCount + fluidIndexCount, IndexFormat.UInt16);

			NativeArray<Vertex> vertexData = meshData.GetVertexData<Vertex>();
			for (var i = 0; i < solidVertexCount; i++)
				vertexData[i] = solidMesh.Vertices[i];
			for (var i = 0; i < fluidVertexCount; i++)
				vertexData[solidVertexCount + i] = fluidMesh.Vertices[i];

			NativeArray<ushort> indexData = meshData.GetIndexData<ushort>();
			for (var i = 0; i < solidIndexCount; i++)
				indexData[i] = (ushort)solidMesh.Triangles[i];
			for (var i = 0; i < fluidIndexCount; i++)
				indexData[solidIndexCount + i] = (ushort)(fluidMesh.Triangles[i] + solidVertexCount);

			meshData.SetSubMesh(0, new SubMeshDescriptor(0, solidIndexCount)
			                       {
				                       firstVertex = 0, vertexCount = solidVertexCount, baseVertex = 0
			                       }, UPDATE_FLAGS);

			meshData.SetSubMesh(1, new SubMeshDescriptor(solidIndexCount, fluidIndexCount)
			                       {
				                       firstVertex = solidVertexCount, vertexCount = fluidVertexCount, baseVertex = 0
			                       }, UPDATE_FLAGS);
		}

		// Fallback for pathological chunks (caves, checkerboard terrain, etc.)
		private void SetMeshDataArrayUInt32(ref Mesh.MeshDataArray meshDataArray,
		                                    NativeMesh             solidMesh,        NativeMesh fluidMesh,
		                                    int                    solidVertexCount, int        fluidVertexCount,
		                                    int                    solidIndexCount,  int        fluidIndexCount)
		{
			Mesh.MeshData meshData = meshDataArray[0];
			meshData.subMeshCount = 2;
			meshData.SetVertexBufferParams(solidVertexCount + fluidVertexCount, Layout);
			meshData.SetIndexBufferParams(solidIndexCount + fluidIndexCount, IndexFormat.UInt32);

			NativeArray<Vertex> vertexData = meshData.GetVertexData<Vertex>();
			for (var i = 0; i < solidVertexCount; i++)
				vertexData[i] = solidMesh.Vertices[i];
			for (var i = 0; i < fluidVertexCount; i++)
				vertexData[solidVertexCount + i] = fluidMesh.Vertices[i];

			NativeArray<int> indexData = meshData.GetIndexData<int>();
			for (var i = 0; i < solidIndexCount; i++)
				indexData[i] = solidMesh.Triangles[i];
			for (var i = 0; i < fluidIndexCount; i++)
				indexData[solidIndexCount + i] = fluidMesh.Triangles[i] + solidVertexCount;

			meshData.SetSubMesh(0, new SubMeshDescriptor(0, solidIndexCount)
			                       {
				                       firstVertex = 0, vertexCount = solidVertexCount, baseVertex = 0
			                       }, UPDATE_FLAGS);
			meshData.SetSubMesh(1, new SubMeshDescriptor(solidIndexCount, fluidIndexCount)
			                       {
				                       firstVertex = solidVertexCount, vertexCount = fluidVertexCount, baseVertex = 0
			                       }, UPDATE_FLAGS);
		}
	}
}