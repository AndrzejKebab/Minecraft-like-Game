using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(CompileSynchronously = true)]
public struct ChunkJob : IJob
{
	public struct MeshData
	{
		public NativeList<Vertex> Vertex;
		public NativeList<ushort> MeshTriangles;
	}

	public struct BlockData
	{
		public NativeArray<half4> BlockVertices;
		public NativeArray<int> BlockTriangles;
		public NativeArray<float2> BlockUVs;
		public NativeArray<int3> BlockFaceChecks;
	}

	public struct ChunkData
	{
		public NativeArray<BlockTypesJob> BlockTypes;
		public BiomeAttributesJob BiomeData;
		public NativeArray<ushort> VoxelMap;
	}

	[WriteOnly]
	public MeshData meshData;
	[ReadOnly]
	public ChunkData chunkData;
	[ReadOnly]
	public BlockData blockData;

	private ushort vertexIndex;
	[ReadOnly] public int ChunkSize;
	[ReadOnly] public int TextureAtlasSize;
	[ReadOnly] public float NormalizedTextureAtlas;
	[ReadOnly] public int WorldSizeInVoxels;
	[ReadOnly] public int3 Position;

	public void Execute()
	{
		CreateMeshData();
	}

	public void CreateMeshData()
	{
		for (int y = 0; y < ChunkSize; y++)
		{
			for (int x = 0; x < ChunkSize; x++)
			{
				for (int z = 0; z < ChunkSize; z++)
				{
					if (chunkData.BlockTypes[chunkData.VoxelMap[WorldExtensions.FlattenIndex(x, y, z, ChunkSize)]].IsSolid)
					{
						AddVoxelDataToChunk(new int3(x, y, z));
					}
				}
			}
		}
	}

	public void AddVoxelDataToChunk(int3 pos)
	{
		for (int p = 0; p < 6; p++)
		{
			if (!CheckVoxel(pos + blockData.BlockFaceChecks[p]))
			{
				int posX = pos.x;
				int posY = pos.y;
				int posZ = pos.z;
				ushort blockID = chunkData.BlockTypes[chunkData.VoxelMap[WorldExtensions.FlattenIndex(posX, posY, posZ, ChunkSize)]].BlockID;
				
				meshData.MeshTriangles.Add(vertexIndex);
				meshData.MeshTriangles.Add((ushort)(vertexIndex + 1));
				meshData.MeshTriangles.Add((ushort)(vertexIndex + 3));
				meshData.MeshTriangles.Add((ushort)(vertexIndex + 2));

				var _vertices = GetFaceVertices(p, new half4((half)pos.x, (half)pos.y, (half)pos.z, (half)0));
				var _textureUVs = GetTextureUVs(chunkData.BlockTypes[blockID].GetTexture2D(p));

				half4 normal = new half4((half)blockData.BlockFaceChecks[p].x,
										 (half)blockData.BlockFaceChecks[p].y,
										 (half)blockData.BlockFaceChecks[p].z,
										 (half)0);

				Color32 tangent = new Color32((byte)normal.x, (byte)normal.y, (byte)normal.z, (byte)normal.w);

				meshData.Vertex.Add(new Vertex(_vertices[0], normal, tangent, _textureUVs[0]));
				meshData.Vertex.Add(new Vertex(_vertices[1], normal, tangent, _textureUVs[1]));
				meshData.Vertex.Add(new Vertex(_vertices[2], normal, tangent, _textureUVs[2]));
				meshData.Vertex.Add(new Vertex(_vertices[3], normal, tangent, _textureUVs[3]));

				_textureUVs.Dispose();
				_vertices.Dispose();
				vertexIndex += 4;
			}
		}
	}

	private NativeArray<half4> GetFaceVertices(int faceIndex, half4 pos)
	{
		var _faceVertices = new NativeArray<half4>(4, Allocator.Temp);

		for (byte i = 0; i < 4; i++)
		{
			var _index = blockData.BlockTriangles[(faceIndex * 4) + i];
			_faceVertices[i] = new half4(new half(blockData.BlockVertices[_index].x + pos.x),
										 new half(blockData.BlockVertices[_index].y + pos.y),
										 new half(blockData.BlockVertices[_index].z + pos.z),
										(half)0);
		}

		return _faceVertices;
	}

	private NativeArray<half2> GetTextureUVs(int textureID)
	{
		var _textureUVs = new NativeArray<half2>(4, Allocator.Temp);

		float y = textureID / TextureAtlasSize;
		float x = textureID - (y * TextureAtlasSize);

		x *= NormalizedTextureAtlas;
		y *= NormalizedTextureAtlas;

		y = 1f - y - NormalizedTextureAtlas;

		_textureUVs[0] = new half2((half)x, (half)y);
		_textureUVs[1] = new half2((half)x, new half(y + NormalizedTextureAtlas));
		_textureUVs[2] = new half2(new half(x + NormalizedTextureAtlas), (half)y);
		_textureUVs[3] = new half2(new half(x + NormalizedTextureAtlas), new half(y + NormalizedTextureAtlas));

		return _textureUVs;
	}

	private bool IsVoxelInChunk(int3 pos)
	{
		if (pos.x < 0 || pos.x > ChunkSize - 1 ||
			pos.y < 0 || pos.y > ChunkSize - 1 ||
			pos.z < 0 || pos.z > ChunkSize - 1)
		{
			return false;
		}
		else
		{
			return true;
		}
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
			int posX = pos.x;
			int posY = pos.y;
			int posZ = pos.z;
			return chunkData.BlockTypes[chunkData.VoxelMap[WorldExtensions.FlattenIndex(posX, posY, posZ, ChunkSize)]].IsSolid;
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