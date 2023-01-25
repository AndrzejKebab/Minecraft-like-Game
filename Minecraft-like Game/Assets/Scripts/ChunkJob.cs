using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously = true)]
public struct ChunkJob : IJob
{
	public struct MeshData
	{
		public NativeList<int3> Vertices;
		public NativeList<int> Triangles;
	}

	public struct BlockData
	{
		public NativeArray<int3> Vertices;
		public NativeArray<int> Triangles;
	}

	public struct ChunkData
	{
		public NativeArray<Block> Blocks;
	}

	[WriteOnly]
	public MeshData meshData;
	[ReadOnly]
	public ChunkData chunkData;
	[ReadOnly]
	public  BlockData blockData;

	private int vertexCount;
	
	public void Execute()
	{
		vertexCount = 0;
		for (int x = 0; x < 16; x++)
		{
			for (int z = 0; z < 16; z++)
			{
				for (int y = 0; y < 16; y++)
				{
					if (chunkData.Blocks[BlockExtensions.GetBlockIndex(new int3(x, y, z))].IsEmpty()) continue;

					for (int i = 0; i < 6; i++)
					{
						var _direction = (Direction)i;

						if (Check(BlockExtensions.GetPositionInDirection(_direction, x, y, z)))
						{
							CreateFace(_direction, new int3(x, y, z));
						}
					}
				}
			}
		}
	}

	private void CreateFace(Direction direction, int3 pos)
	{
		var _vertices = GetFaceVertices(direction, 1, pos);

		meshData.Vertices.AddRange(_vertices);

		_vertices.Dispose();

		meshData.Triangles.Add(vertexCount);
		meshData.Triangles.Add(vertexCount + 1);
		meshData.Triangles.Add(vertexCount + 2);
		meshData.Triangles.Add(vertexCount);
		meshData.Triangles.Add(vertexCount + 2);
		meshData.Triangles.Add(vertexCount + 3);

		vertexCount += 4;
	}

	private bool Check(int3 position)
	{
		if(position.x >= 16 || position.z >= 16 || position.x < 0 || position.z < 0 || position.y < 0) return true;

		if(position.y >= 16) return false;

		return chunkData.Blocks[BlockExtensions.GetBlockIndex(position)].IsEmpty();
	}

	public NativeArray<int3> GetFaceVertices(Direction direction, int scale, int3 position)
	{
		var _faceVertices = new NativeArray<int3>(4, Allocator.Temp);

		for (int i = 0; i < 4; i++)
		{
			var _index = blockData.Triangles[(int)direction * 4 + i];
			_faceVertices[i] = blockData.Vertices[_index] * scale + position;
		}

		return _faceVertices;
	}
}