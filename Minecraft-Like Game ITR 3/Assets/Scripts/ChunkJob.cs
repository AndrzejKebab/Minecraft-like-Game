using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
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
		public BiomeAttributesJob BiomeData;
		public NativeArray<ushort> VoxelMap;
	}

	[ReadOnly]
	public ChunkData chunkData;
	[WriteOnly]
	public MeshData meshData;

	private ushort vertexIndex;
	[ReadOnly] public int ChunkSize;
	[ReadOnly] public int TextureAtlasSize;
	[ReadOnly] public float NormalizedTextureAtlas;
	[ReadOnly] public int WorldSizeInVoxels;
	[ReadOnly] public int3 Position;

	public void Execute() => CreateMeshData();

	private void CreateMeshData()
	{
		for (var y = 0; y < ChunkSize; y++)
		{
			for (var x = 0; x < ChunkSize; x++)
			{
				for (var z = 0; z < ChunkSize; z++)
				{
					if (chunkData.BlockTypes[chunkData.VoxelMap.GetAtFlatIndex(ChunkSize, x, y, z)].IsSolid)
					{
						AddVoxelDataToChunk(new int3(x, y, z));
					}
				}
			}
		}
	}

	private void AddVoxelDataToChunk(int3 pos)
	{
		for (var p = 0; p < 6; p++)
		{
			if (CheckVoxel(pos + VoxelData.FaceChecks[p])) continue;
			var posX = pos.x;
			var posY = pos.y;
			var posZ = pos.z;
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

			var tangent = new Color32((byte)normal.x, (byte)normal.y, (byte)normal.z, (byte)normal.w);

			meshData.Vertex.Add(new Vertex(vertices[0], normal, tangent, textureUVs[0]));
			meshData.Vertex.Add(new Vertex(vertices[1], normal, tangent, textureUVs[1]));
			meshData.Vertex.Add(new Vertex(vertices[2], normal, tangent, textureUVs[2]));
			meshData.Vertex.Add(new Vertex(vertices[3], normal, tangent, textureUVs[3]));

			textureUVs.Dispose();
			vertices.Dispose();
			vertexIndex += 4;
		}
	}

	private NativeArray<half4> GetFaceVertices(int faceIndex, half4 pos)
	{
		var faceVertices = new NativeArray<half4>(4, Allocator.Temp);

		for (byte i = 0; i < 4; i++)
		{
			var index = VoxelData.VoxelTriangles[(faceIndex * 4) + i];
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
		var x = textureID - (y * TextureAtlasSize);

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
		if (!IsVoxelInChunk(pos))
		{
			float posX = pos.x + Position.x;
			float posY = pos.y + Position.y;
			float posZ = pos.z + Position.z;
			return chunkData.BlockTypes[WorldExtensions.GetVoxel(posX, posY, posZ, WorldSizeInVoxels, chunkData.BiomeData.BiomeScale, chunkData.BiomeData.BiomeHeight, chunkData.BiomeData.SolidGroundHeight)].IsSolid;
		}
		else
		{
			var posX = pos.x;
			var posY = pos.y;
			var posZ = pos.z;
			return chunkData.BlockTypes[chunkData.VoxelMap.GetAtFlatIndex(ChunkSize, posX, posY, posZ)].IsSolid;
		}
	}

	//private bool CheckVoxel(int3 pos)
	//{
	//	if (!IsVoxelInChunk(pos))
	//	{
	//		float3 _pos = math.float3(pos + Position);
	//		return World.instance.CheckForVoxel(_pos);
	//	}
	//
	//	return chunkData.BlockTypes[chunkData.VoxelMap[WorldExtensions.FlattenIndex(pos.x, pos.y, pos.z, ChunkSize)]].IsSolid;
	//}
}