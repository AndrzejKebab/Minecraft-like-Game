using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously = true)]
public struct ChunkJob : IJob
{
	public struct MeshData
	{
		public NativeList<int3> MeshVertices;
		public NativeList<int> MeshTriangles;
		public NativeList<float2> MeshUVs;
	}

	public struct BlockData
	{
		public NativeArray<int3> BlockVertices;
		public NativeArray<int> BlockTriangles;
		public NativeArray<float2> BlockUVs;
		public NativeArray<int3> BlockFaceChecks;
	}

	public struct ChunkData
	{
		public NativeArray<short> VoxelMap;
		public NativeArray<BlockType> BlockTypes;
		public int BiomeHeight;
		public int SolidBiomeHeight;
		public int BiomeScale;
	}

	[WriteOnly] 
	public MeshData meshData;
	[ReadOnly] 
	public ChunkData chunkData;
	[ReadOnly] 
	public BlockData blockData;

	private int vertexIndex;
	public int ChunkWidth;
	public int ChunkHeight;
	public int TextureAtlasSize;
	public float NormalizedTextureAtlas;
	public int WorldSizeInVoxels;
	public int3 Position;

	public void Execute()
	{
		CreateMeshData();
	}
	
	private void CreateMeshData()
	{
		for (int y = 0; y < ChunkHeight; y++)
		{
			for (int x = 0; x < ChunkWidth; x++)
			{
				for (int z = 0; z < ChunkWidth; z++)
				{
					if (chunkData.BlockTypes[chunkData.VoxelMap[x + ChunkWidth * (y + ChunkHeight * z)]].IsSolid)
					{
						AddVoxelDataToChunk(new int3(x, y, z));
					}
				}
			}
		}
	}

	private bool IsVoxelInChunk(int3 pos)
	{
		if (pos.x < 0 || pos.x > ChunkWidth - 1 ||
		    pos.y < 0 || pos.y > ChunkHeight - 1 ||
		    pos.z < 0 || pos.z > ChunkWidth - 1)
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
			return chunkData.BlockTypes[WorldExtensions.GetVoxel(pos + Position, ChunkHeight, WorldSizeInVoxels, new int3(chunkData.SolidBiomeHeight, chunkData.BiomeHeight, chunkData.BiomeScale))].IsSolid;
		}
		else
		{
			return chunkData.BlockTypes[chunkData.VoxelMap[pos.x + ChunkWidth * (pos.y + ChunkHeight * pos.z)]].IsSolid;
		}
	}

	private void AddVoxelDataToChunk(int3 pos)
	{
		for (int p = 0; p < 6; p++)
		{
			if (!CheckVoxel(pos + blockData.BlockFaceChecks[p]))
			{
				var blockID = chunkData.BlockTypes[chunkData.VoxelMap[pos.x + ChunkWidth * (pos.y + ChunkHeight * pos.z)]].BlockID;
				var _vertices = GetFaceVertices(p, pos);

				meshData.MeshVertices.AddRange(_vertices);

				_vertices.Dispose();

				AddTexture(chunkData.BlockTypes[blockID].GetTexture2D(p));

				meshData.MeshTriangles.Add(vertexIndex);
				meshData.MeshTriangles.Add(vertexIndex + 1);
				meshData.MeshTriangles.Add(vertexIndex + 2);
				meshData.MeshTriangles.Add(vertexIndex + 2);
				meshData.MeshTriangles.Add(vertexIndex + 1);
				meshData.MeshTriangles.Add(vertexIndex + 3);

				vertexIndex += 4;
			}
		}
	}

	private NativeArray<int3> GetFaceVertices(int faceIndex, int3 pos)
	{
		var _faceVertices = new NativeArray<int3>(4, Allocator.Temp);

		for (int i = 0; i < 4; i++)
		{
			var _index = blockData.BlockTriangles[faceIndex * 4 + i];
			_faceVertices[i] = blockData.BlockVertices[_index] + pos;
		}

		return _faceVertices;
	}

	private void AddTexture(int textureID)
	{
		float y = textureID / TextureAtlasSize;
		float x = textureID - (y * TextureAtlasSize);

		x *= NormalizedTextureAtlas;
		y *= NormalizedTextureAtlas;

		y = 1f - y - NormalizedTextureAtlas;

		meshData.MeshUVs.Add(new float2(x, y));
		meshData.MeshUVs.Add(new float2(x, y + NormalizedTextureAtlas));
		meshData.MeshUVs.Add(new float2(x + NormalizedTextureAtlas, y));
		meshData.MeshUVs.Add(new float2(x + NormalizedTextureAtlas, y + NormalizedTextureAtlas));
	}
}