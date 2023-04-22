using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously = true)]
public struct ChunkJob : IJob
{
	public struct MeshData
	{
		public NativeList<half4> MeshVertices;
		public NativeList<ushort> MeshTriangles;
		public NativeList<int3> Normals;
		public NativeList<int3> Tangents;
		public NativeList<float2> MeshUVs;
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
				var _vertices = GetFaceVertices(p, new half4((half)pos.x, (half)pos.y, (half)pos.z, (half)0));


				meshData.MeshVertices.AddRange(_vertices);

				_vertices.Dispose();

				AddTexture(chunkData.BlockTypes[blockID].GetTexture2D(p));

				meshData.MeshTriangles.Add(vertexIndex);
				meshData.MeshTriangles.Add((ushort)(vertexIndex + 1));
				meshData.MeshTriangles.Add((ushort)(vertexIndex + 3));
				meshData.MeshTriangles.Add((ushort)(vertexIndex + 2));
				
				meshData.Normals.Add(blockData.BlockFaceChecks[p]);
				meshData.Normals.Add(blockData.BlockFaceChecks[p]);
				meshData.Normals.Add(blockData.BlockFaceChecks[p]);
				meshData.Normals.Add(blockData.BlockFaceChecks[p]);

				meshData.Tangents.Add(blockData.BlockFaceChecks[p]);
				meshData.Tangents.Add(blockData.BlockFaceChecks[p]);
				meshData.Tangents.Add(blockData.BlockFaceChecks[p]);
				meshData.Tangents.Add(blockData.BlockFaceChecks[p]);

				vertexIndex += 4;
			}
		}
	}

	private NativeArray<half4> GetFaceVertices(int faceIndex, half4 pos)
	{
		var _faceVertices = new NativeArray<half4>(4, Allocator.Temp);

		for (byte i = 0; i < 4; i++)
		{
			var _index = blockData.BlockTriangles[faceIndex * 4 + i];
			_faceVertices[i] = new half4(new half(blockData.BlockVertices[_index].x + pos.x),
										 new half(blockData.BlockVertices[_index].y + pos.y),
										 new half(blockData.BlockVertices[_index].z + pos.z),
										(half)0);
		}

		return _faceVertices;
	}

	private void AddTexture(int textureID)
	{
		float y = textureID / TextureAtlasSize;
		float x = textureID - (y * TextureAtlasSize);

		x *= NormalizedTextureAtlas;
		y *= NormalizedTextureAtlas;

		float offset = 0.0005f;

		y = 1f - y - NormalizedTextureAtlas;

		meshData.MeshUVs.Add(new float2(x, y) + new float2(offset, offset));
		meshData.MeshUVs.Add(new float2(x, y + NormalizedTextureAtlas) + new float2(offset, -offset));
		meshData.MeshUVs.Add(new float2(x + NormalizedTextureAtlas, y) + new float2(-offset, offset));
		meshData.MeshUVs.Add(new float2(x + NormalizedTextureAtlas, y + NormalizedTextureAtlas) - new float2(offset, offset));
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
	//		var _pos = math.float3(pos + Position);
	//		return WorldExtensions.CheckForVoxel(_pos);
	//	}
	//
	//	return chunkData.BlockTypes[chunkData.VoxelMap[WorldExtensions.FlattenIndex(pos.x, pos.y, pos.z, ChunkSize)]].IsSolid;
	//}
}