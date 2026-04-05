using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public struct BuildMeshJob : IJob
	{
		[ReadOnly] public NativeArray<ushort> Blocks;
		[ReadOnly] public NativeArray<Block>  BlockPrototypes;

		[NativeDisableContainerSafetyRestriction]
		[ReadOnly] public NativeArray<NativeVoxelMeshData> Meshes;

		[ReadOnly] public NativeArray<ushort> NeighborZNeg;
		[ReadOnly] public NativeArray<ushort> NeighborZPos; 
		[ReadOnly] public NativeArray<ushort> NeighborYNeg; 
		[ReadOnly] public NativeArray<ushort> NeighborYPos; 
		[ReadOnly] public NativeArray<ushort> NeighborXNeg; 
		[ReadOnly] public NativeArray<ushort> NeighborXPos;
		
		public            int                 ChunkSize;

		public NativeMesh SolidMesh;
		public NativeMesh TransparentMesh;
		public NativeMesh FluidMesh;

		public void Execute()
		{
			if (Meshes.Length == 0) return;

			for (var x = 0; x < ChunkSize; x++)
			for (var y = 0; y < ChunkSize; y++)
			for (var z = 0; z < ChunkSize; z++)
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

					int3 dir    = ChunkMeshBuilderSystem.GetFaceDirection(i);
					var  normal = new float3(dir.x, dir.y, dir.z);

					if (NeighbourHidesFace(x, y, z, dir, block.IsTransparent))
						continue;

					var wPos       = new float3(x, y, z);
					var texBase    = GetTextureIndex(normal, block.BaseTextures);
					var texOverlay = GetTextureIndex(normal, block.OverlayTextures);

					Vertex vert0 = CreateVertex(v0 + wPos, normal, 0, 0, texBase, texOverlay);
					Vertex vert1 = CreateVertex(v1 + wPos, normal, 0, 1, texBase, texOverlay);
					Vertex vert2 = CreateVertex(v2 + wPos, normal, 1, 0, texBase, texOverlay);
					Vertex vert3 = CreateVertex(v3 + wPos, normal, 1, 1, texBase, texOverlay);

					if (block.IsFluid) AddFace(vert0, vert1, vert2, vert3, ref FluidMesh);
					else if (block.IsTransparent)
						AddFace(vert0, vert1, vert2, vert3, ref TransparentMesh);
					else AddFace(vert0, vert1, vert2, vert3, ref SolidMesh);
				}
			}
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

		private static Vertex CreateVertex(float3 pos, float3 norm, float u, float v, ushort tBase, ushort tOver)
		{
			return new Vertex
			       {
				       position        = pos, normal = norm, texCoordBase = new float3(u, v, tBase),
				       texCoordOverlay = new float3(u, v, tOver)
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
	}
}