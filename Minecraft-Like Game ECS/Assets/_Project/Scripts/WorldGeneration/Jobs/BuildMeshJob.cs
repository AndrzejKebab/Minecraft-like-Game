using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.WorldGeneration.Jobs
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance,
		             FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public struct BuildMeshJob : IJob
	{
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

		[ReadOnly] public NativeArray<float3> FaceChecks;
		[ReadOnly] public NativeArray<float3> FaceTangents;
		public            int                 ChunkSize;

		// Grid coordinate (in chunk units) of this chunk.
		// Stored in every vertex so the shader can reconstruct world-space origin
		// without relying on SV_InstanceID or Unity's indirect draw constant system.
		public int3 ChunkCoord;

		public NativeMesh SolidMesh;
		public NativeMesh FluidMesh;

		public void Execute()
		{
			if (Meshes.Length == 0) return;

			for (ushort x = 0; x < ChunkSize; x++)
			for (ushort y = 0; y < ChunkSize; y++)
			for (ushort z = 0; z < ChunkSize; z++)
			{
				BlockState blockState = Blocks[x | (y << 5) | (z << 10)];
				if (blockState.IsEmpty) continue;

				Block block = BlockPrototypes[blockState.ID];
				if (block.MeshID >= Meshes.Length) continue;

				NativeVoxelMeshData meshData = Meshes[block.MeshID];
				quaternion          rot      = GetRotation(block.DirectionType, blockState.Orientation);
				var                 wPos     = new float3(x, y, z);

				for (var i = 0; i < meshData.Triangles.Length; i++)
				{
					int4   quad            = meshData.Triangles[i];
					float3 originalNormal  = FaceChecks[i];
					float3 originalTangent = FaceTangents[i];

					float3 rotatedNormal = math.round(math.mul(rot, originalNormal));
					var    dir           = new int3(rotatedNormal);

					if (NeighbourHidesFace(x, y, z, dir, block.IsTransparent)) continue;

					float3 v0 = math.mul(rot, meshData.Vertices[quad.x] - 0.5f) + 0.5f + wPos;
					float3 v1 = math.mul(rot, meshData.Vertices[quad.y] - 0.5f) + 0.5f + wPos;
					float3 v2 = math.mul(rot, meshData.Vertices[quad.z] - 0.5f) + 0.5f + wPos;
					float3 v3 = math.mul(rot, meshData.Vertices[quad.w] - 0.5f) + 0.5f + wPos;

					float3 rotatedTangent    = math.round(math.mul(rot, originalTangent));

					var tBase     = GetTextureIndex(originalNormal, block.BaseTextures);
					var tOverlay  = GetTextureIndex(originalNormal, block.OverlayTextures);
					var tNormal   = GetTextureIndex(originalNormal, block.NormalTextures);
					var tSpecular = GetTextureIndex(originalNormal, block.SpecularTextures);

					Vertex vert0 = new (v0, rotatedNormal, rotatedTangent, block.TintColor, 0f, 0f, tBase, tOverlay, tNormal, tSpecular, ChunkCoord);
					Vertex vert1 = new (v1, rotatedNormal, rotatedTangent, block.TintColor, 0f, 1f, tBase, tOverlay, tNormal, tSpecular, ChunkCoord);
					Vertex vert2 = new (v2, rotatedNormal, rotatedTangent, block.TintColor, 1f, 0f, tBase, tOverlay, tNormal, tSpecular, ChunkCoord);
					Vertex vert3 = new (v3, rotatedNormal, rotatedTangent, block.TintColor, 1f, 1f, tBase, tOverlay, tNormal, tSpecular, ChunkCoord);

					if (block.IsFluid) AddFace(vert0, vert1, vert2, vert3, ref FluidMesh);
					else               AddFace(vert0, vert1, vert2, vert3, ref SolidMesh);
				}
			}
		}

		private static Vertex MakeVertex(float3  pos,   float3 norm,     float4 tangent,
		                                 Color32 color, float  u,        float  v,
		                                 ushort  tBase, ushort tOverlay, ushort tNorm, ushort tSpec,
		                                 int3    chunkCoord)
		{
			var posX    = (uint)math.round(math.clamp(pos.x * 16f, 0f, 1023f));
			var posY    = (uint)math.round(math.clamp(pos.y * 16f, 0f, 1023f));
			var posZ    = (uint)math.round(math.clamp(pos.z * 16f, 0f, 1023f));
			var tanSign = tangent.w >= 0f ? 1u : 0u;

			var data1 = posX | (posY << 10) | (posZ << 20) | (tanSign << 30);

			var data2 = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

			var uvX = (uint)math.round(math.clamp(u * 256f, 0f, 511f));
			var uvY = (uint)math.round(math.clamp(v * 256f, 0f, 511f));

			var normIdx = DirToIndex(norm);
			var tanIdx  = DirToIndex(tangent.xyz);

			uint uBase    = tBase;
			uint uOverlay = tOverlay;
			uint uNorm    = tNorm;
			uint uSpec    = tSpec;

			var data3 = (uBase & 0x3FFu) | ((uOverlay & 0x3FFu) << 10) | (uvX << 20) | (normIdx << 29);
			var data4 = (uNorm & 0x3FFu) | ((uSpec    & 0x3FFu) << 10) | (uvY << 20) | (tanIdx  << 29);

			// Pack chunk grid coordinates into data5 (10 bits each, biased by +512).
			// The shader decodes these to reconstruct world-space chunk origin,
			// eliminating any dependency on SV_InstanceID or Unity's indirect draw system.
			// Supports chunk grid coords -512..+511 per axis (world ±16 384 units).
			var cx    = (uint)(chunkCoord.x + 512) & 0x3FFu;
			var cy    = (uint)(chunkCoord.y + 512) & 0x3FFu;
			var cz    = (uint)(chunkCoord.z + 512) & 0x3FFu;
			var data5 = cx | (cy << 10) | (cz << 20);

			return new Vertex
			       {
				       Data1 = data1,
				       Data2 = data2,
				       Data3 = data3,
				       Data4 = data4,
				       Data5 = data5
			       };
		}

		private static uint DirToIndex(float3 dir)
		{
			return dir.z switch
			       {
				       < -0.5f => 0,
				       > 0.5f  => 1,
				       _ => dir.y switch
				            {
					            > 0.5f  => 2,
					            < -0.5f => 3,
					            _       => dir.x < -0.5f ? 4 : (uint)5
				            }
			       };
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
				if (NeighborYPos.IsCreated) nbId = NeighborYPos[nx | (nz << 10)].ID;
			}
			else if (nz < 0)
			{
				if (NeighborZNeg.IsCreated) nbId = NeighborZNeg[nx | (ny << 5) | ((ChunkSize - 1) << 10)].ID;
			}
			else if (nz >= ChunkSize)
			{
				if (NeighborZPos.IsCreated) nbId = NeighborZPos[nx | (ny << 5)].ID;
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
					            _       => normal.z > 0.5f ? layer.Front : layer.Back
				            }
			       };
		}

		private static quaternion GetRotation(BlockDirectionType type, byte orientation)
		{
			return type switch
			       {
				       BlockDirectionType.None => quaternion.identity,
				       BlockDirectionType.YAxis => orientation switch
				                                   {
					                                   2 => quaternion.identity,
					                                   4 => quaternion.Euler(0, math.PI / 2f, 0),
					                                   3 => quaternion.Euler(0, math.PI, 0),
					                                   5 => quaternion.Euler(0, -math.PI / 2f, 0),
					                                   _ => quaternion.identity
				                                   },
				       BlockDirectionType.AllAxes => orientation switch
				                                     {
					                                     0 => quaternion.identity,
					                                     1 => quaternion.Euler(math.PI, 0, 0),
					                                     2 => quaternion.Euler(math.PI / 2f, 0, 0),
					                                     3 => quaternion.Euler(-math.PI / 2f, 0, 0),
					                                     4 => quaternion.Euler(0, 0, -math.PI / 2f),
					                                     5 => quaternion.Euler(0, 0, math.PI / 2f),
					                                     _ => quaternion.identity
				                                     },
				       _ => quaternion.identity
			       };
		}
	}
}