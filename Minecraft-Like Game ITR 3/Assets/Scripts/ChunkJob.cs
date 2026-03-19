using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UtilityLibrary.Unity.Runtime;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct ChunkJob : IJob
{
	public struct MeshData
	{
		public NativeList<Vertex> Vertex;
		public NativeList<ushort> MeshTriangles;
	}

	public struct ChunkData
	{
		public NativeArray<BlockTypesJob> BlockTypes;
		public BiomeAttributesJob         BiomeData;
		public NativeArray<ushort>        VoxelMap;

		// Neighbor voxel maps, indexed by VoxelData.FaceChecks order:
		// 0 = Z- (0,0,-1), 1 = Z+ (0,0,1), 2 = Y+ (0,1,0),
		// 3 = Y- (0,-1,0), 4 = X- (-1,0,0), 5 = X+ (1,0,0)
		public NativeArray<ushort> NeighborZNeg;
		public NativeArray<ushort> NeighborZPos;
		public NativeArray<ushort> NeighborYPos;
		public NativeArray<ushort> NeighborYNeg;
		public NativeArray<ushort> NeighborXNeg;
		public NativeArray<ushort> NeighborXPos;

		public bool HasNeighborZNeg;
		public bool HasNeighborZPos;
		public bool HasNeighborYPos;
		public bool HasNeighborYNeg;
		public bool HasNeighborXNeg;
		public bool HasNeighborXPos;
	}

	[ReadOnly] public ChunkData chunkData;
	public            MeshData  meshData;

	[WriteOnly] [NativeDisableUnsafePtrRestriction]
	public IntPtr nodeHandle;

	public Mesh.MeshDataArray                     MeshDataArray;
	public NativeArray<VertexAttributeDescriptor> Layout;

	private           ushort vertexIndex;
	[ReadOnly] public int    ChunkSize;
	[ReadOnly] public int    TextureAtlasSize;
	[ReadOnly] public float  NormalizedTextureAtlas;
	[ReadOnly] public int    WorldSizeInVoxels;
	[ReadOnly] public int3   Position;

	public void Execute()
	{
		CreateMeshData();
	}

	private void CreateMeshData()
	{
		for (var y = 0; y < ChunkSize; y++)
		for (var x = 0; x < ChunkSize; x++)
		for (var z = 0; z < ChunkSize; z++)
			if (chunkData.BlockTypes[chunkData.VoxelMap.GetAtFlatIndex(ChunkSize, x, y, z)].IsSolid)
				AddVoxelDataToChunk(new int3(x, y, z));

		Mesh.MeshData data = MeshDataArray[0];
		data.subMeshCount = 1;
		data.SetIndexBufferParams(meshData.MeshTriangles.Length, IndexFormat.UInt16);
		NativeArray<ushort> index = data.GetIndexData<ushort>();
		index.CopyFrom(meshData.MeshTriangles.AsArray());
		data.SetVertexBufferParams(meshData.Vertex.Length, Layout);
		NativeArray<Vertex> vertex = data.GetVertexData<Vertex>();
		vertex.CopyFrom(meshData.Vertex.AsArray());

		var desc = new SubMeshDescriptor(0, meshData.MeshTriangles.Length, MeshTopology.Quads);
		data.SetSubMesh(0, desc, MeshUpdateFlags.DontRecalculateBounds);
	}

	private void AddVoxelDataToChunk(int3 pos)
	{
		for (var p = 0; p < 6; p++)
		{
			if (CheckVoxel(pos + VoxelData.FaceChecks[p])) continue;
			var posX    = pos.x;
			var posY    = pos.y;
			var posZ    = pos.z;
			var blockID = chunkData.BlockTypes[chunkData.VoxelMap.GetAtFlatIndex(ChunkSize, posX, posY, posZ)].BlockID;

			meshData.MeshTriangles.Add(vertexIndex);
			meshData.MeshTriangles.Add((ushort)(vertexIndex + 1));
			meshData.MeshTriangles.Add((ushort)(vertexIndex + 3));
			meshData.MeshTriangles.Add((ushort)(vertexIndex + 2));

			NativeArray<half4> vertices = GetFaceVertices(p, new half4((half)pos.x, (half)pos.y, (half)pos.z, (half)0));
			NativeArray<half2> textureUVs = GetTextureUVs(chunkData.BlockTypes[blockID].GetTexture2D(p));

			var normal = new sbyte4((sbyte)VoxelData.FaceChecks[p].x,
			                        (sbyte)VoxelData.FaceChecks[p].y,
			                        (sbyte)VoxelData.FaceChecks[p].z,
			                        0);

			var tangent = new sbyte4((sbyte)VoxelData.FaceTangents[p].x,
			                         (sbyte)VoxelData.FaceTangents[p].y,
			                         (sbyte)VoxelData.FaceTangents[p].z,
			                         -1);

			meshData.Vertex.Add(new Vertex(vertices[0], normal, tangent, textureUVs[0]));
			meshData.Vertex.Add(new Vertex(vertices[1], normal, tangent, textureUVs[1]));
			meshData.Vertex.Add(new Vertex(vertices[2], normal, tangent, textureUVs[2]));
			meshData.Vertex.Add(new Vertex(vertices[3], normal, tangent, textureUVs[3]));

			textureUVs.Dispose();
			vertices.Dispose();
			vertexIndex += 4;
		}
	}

	private static NativeArray<half4> GetFaceVertices(int faceIndex, half4 pos)
	{
		var faceVertices = new NativeArray<half4>(4, Allocator.Temp);

		for (byte i = 0; i < 4; i++)
		{
			var index = VoxelData.VoxelTriangles[faceIndex * 4 + i];
			faceVertices[i] = new half4((half)(VoxelData.VoxelVertices[index].x + pos.x),
			                            (half)(VoxelData.VoxelVertices[index].y + pos.y),
			                            (half)(VoxelData.VoxelVertices[index].z + pos.z),
			                            (half)0);
		}

		return faceVertices;
	}

	private NativeArray<half2> GetTextureUVs(int textureID)
	{
		var textureUVs = new NativeArray<half2>(4, Allocator.Temp);

		float y = textureID / TextureAtlasSize;
		var   x = textureID - y * TextureAtlasSize;

		x *= NormalizedTextureAtlas;
		y *= NormalizedTextureAtlas;

		y = 1f - y - NormalizedTextureAtlas;

		textureUVs[0] = new half2((half)x, (half)y);
		textureUVs[1] = new half2((half)x, new half(y + NormalizedTextureAtlas));
		textureUVs[2] = new half2(new half(x + NormalizedTextureAtlas), (half)y);
		textureUVs[3] = new half2(new half(x + NormalizedTextureAtlas), new half(y + NormalizedTextureAtlas));

		return textureUVs;
	}

	private bool IsVoxelInChunk(int3 pos)
	{
		return pos.x >= 0 && pos.x <= ChunkSize - 1 &&
		       pos.y >= 0 && pos.y <= ChunkSize - 1 &&
		       pos.z >= 0 && pos.z <= ChunkSize - 1;
	}

	private bool CheckVoxel(int3 pos)
	{
		if (IsVoxelInChunk(pos))
			return chunkData.BlockTypes[
			                            chunkData.VoxelMap.GetAtFlatIndex(ChunkSize, pos.x, pos.y, pos.z)
			                           ].IsSolid;
		// Try to read from a loaded neighbor's voxel map first so player
		// edits near chunk borders are reflected correctly. Each out-of-bounds
		// axis maps to exactly one neighbor face (FaceChecks are unit vectors,
		// so only one axis can be out of range at a time).

		if (pos.z < 0 && chunkData.HasNeighborZNeg)
			return chunkData.BlockTypes[
			                            chunkData.NeighborZNeg.GetAtFlatIndex(ChunkSize, pos.x, pos.y,
			                                                                  ChunkSize - 1)
			                           ].IsSolid;

		if (pos.z >= ChunkSize && chunkData.HasNeighborZPos)
			return chunkData.BlockTypes[
			                            chunkData.NeighborZPos.GetAtFlatIndex(ChunkSize, pos.x, pos.y, 0)
			                           ].IsSolid;

		if (pos.y >= ChunkSize && chunkData.HasNeighborYPos)
			return chunkData.BlockTypes[
			                            chunkData.NeighborYPos.GetAtFlatIndex(ChunkSize, pos.x, 0, pos.z)
			                           ].IsSolid;

		if (pos.y < 0 && chunkData.HasNeighborYNeg)
			return chunkData.BlockTypes[
			                            chunkData.NeighborYNeg.GetAtFlatIndex(ChunkSize, pos.x, ChunkSize - 1,
			                                                                  pos.z)
			                           ].IsSolid;

		if (pos.x < 0 && chunkData.HasNeighborXNeg)
			return chunkData.BlockTypes[
			                            chunkData.NeighborXNeg.GetAtFlatIndex(ChunkSize, ChunkSize - 1, pos.y,
			                                                                  pos.z)
			                           ].IsSolid;

		if (pos.x >= ChunkSize && chunkData.HasNeighborXPos)
			return chunkData.BlockTypes[
			                            chunkData.NeighborXPos.GetAtFlatIndex(ChunkSize, 0, pos.y, pos.z)
			                           ].IsSolid;

		// Neighbor not loaded yet — fall back to noise-based generation.
		float posX = pos.x + Position.x;
		float posY = pos.y + Position.y;
		float posZ = pos.z + Position.z;
		return chunkData.BlockTypes[WorldExtensions.GetVoxel(
		                                                     nodeHandle, posX, posY, posZ,
		                                                     WorldSizeInVoxels,
		                                                     chunkData.BiomeData.BiomeScale,
		                                                     chunkData.BiomeData.BiomeHeight,
		                                                     chunkData.BiomeData.SolidGroundHeight)].IsSolid;

	}
}