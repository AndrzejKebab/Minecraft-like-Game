using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.WorldGeneration.Jobs
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public struct GreedyMeshJob : IJob
	{
		[ReadOnly] public ChunkAccessor Accessor;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeArray<Block> BlockPrototypes;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeArray<NativeVoxelMeshData> CustomMeshes;

		[ReadOnly] public NativeArray<float3> FaceChecks;
		[ReadOnly] public NativeArray<float3> FaceTangents;

		public NativeMesh SolidMesh;
		public NativeMesh FluidMesh;

		private struct Mask
		{
			public ushort BlockID;
			public byte   MeshType;
			public sbyte  Normal;
			public int4   AO;
		}

		public void Execute()
		{
			var chunkSize = Accessor.ChunkSize;
			var maskFront = new NativeArray<Mask>(chunkSize * chunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			var maskBack  = new NativeArray<Mask>(chunkSize * chunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			Mask mFront = default;
			Mask mBack  = default;
			// 1. GREEDY MESHING (Standard Cubes & Fluids)
			for (var direction = 0; direction < 3; direction++)
			{
				var axis1 = (direction + 1) % 3;
				var axis2 = (direction + 2) % 3;

				int3 chunkItr      = int3.zero;
				int3 directionMask = int3.zero;
				directionMask[direction] = 1;



				for (chunkItr[direction] = -1; chunkItr[direction] < chunkSize;)
				{
					var n = 0;
					for (chunkItr[axis2] = 0; chunkItr[axis2] < chunkSize; chunkItr[axis2]++)
					for (chunkItr[axis1] = 0; chunkItr[axis1] < chunkSize; chunkItr[axis1]++)
					{
						BlockState current = Accessor.GetBlockState(chunkItr);
						BlockState compare = Accessor.GetBlockState(chunkItr + directionMask);

						var currentType = GetMeshType(current);
						var compareType = GetMeshType(compare);

						var currentTransparent = IsTransparent(current);
						var compareTransparent = IsTransparent(compare);



						// Face of current block (facing positive)
						if (currentType != 0 && currentType != 3)
						{
							var faceVisible                                                 = compareTransparent;
							if (currentType == compareType && currentType == 2) faceVisible = false; // Water-Water cull

							if (faceVisible)
							{
								mFront.BlockID  = current.ID;
								mFront.MeshType = currentType;
								mFront.Normal   = -1;
								mFront.AO       = new int4(3, 3, 3, 3);
							}
						}

						// Face of compare block (facing negative)
						if (compareType != 0 && compareType != 3)
						{
							var faceVisible                                                 = currentTransparent;
							if (compareType == currentType && compareType == 2) faceVisible = false; // Water-Water cull

							if (faceVisible)
							{
								mBack.BlockID  = compare.ID;
								mBack.MeshType = compareType;
								mBack.Normal   = 1;
								mBack.AO       = new int4(3, 3, 3, 3);
							}
						}

						maskFront[n] = mFront;
						maskBack[n]  = mBack;
						n++;
					}

					chunkItr[direction]++;

					ProcessMask(maskFront, direction, axis1, axis2, chunkSize, chunkItr);
					ProcessMask(maskBack, direction, axis1, axis2, chunkSize, chunkItr);
				}
			}
			
			maskFront.Dispose();
			maskBack.Dispose();
			
			// 2. CUSTOM MESHING (Slabs, Fences, Foliage)
			for (var x = 0; x < chunkSize; x++)
			for (var y = 0; y < chunkSize; y++)
			for (var z = 0; z < chunkSize; z++)
			{
				BlockState state = Accessor.GetBlockState(x, y, z);
				if (state.IsEmpty || GetMeshType(state) != 3) continue;

				RenderCustomMesh(x, y, z, state);
			}
		}

		private void ProcessMask(NativeArray<Mask> mask, int direction, int axis1, int axis2, int chunkSize, int3 chunkItr)
		{
			var n = 0;
			for (var j = 0; j < chunkSize; j++)
			for (var i = 0; i < chunkSize;)
				if (mask[n].MeshType != 0)
				{
					Mask currentMask = mask[n];
					int3 basePos     = chunkItr;
					basePos[axis1] = i;
					basePos[axis2] = j;

					int width;
					for (width = 1; i + width < chunkSize && CompareMask(mask[n + width], currentMask); width++)
					{
					}

					int height;
					var done = false;
					for (height = 1; j + height < chunkSize; height++)
					{
						for (var k = 0; k < width; k++)
						{
							if (CompareMask(mask[n + k + height * chunkSize], currentMask)) continue;
							done = true;
							break;
						}

						if (done) break;
					}

					CreateGreedyQuad(currentMask, direction, axis1, axis2, width, height, basePos);

					for (var l = 0; l < height; l++)
					for (var k = 0; k < width; k++)
						mask[n + k + l * chunkSize] = default;

					i += width;
					n += width;
				}
				else
				{
					i++;
					n++;
				}
		}

		private static bool CompareMask(Mask a, Mask b)
		{
			return a.MeshType == b.MeshType && a.BlockID == b.BlockID && a.Normal == b.Normal && a.AO.Equals(b.AO);
		}

		private byte GetMeshType(BlockState state)
		{
			if (state.IsEmpty || state.ID == 0) return 0; // Air
			Block block = BlockPrototypes[state.ID];
			if (block.IsFluid) return 2;         // Fluid
			return block.MeshID == 0 ?	(byte)1 : // Standard Solid Cube
										(byte)3; // Custom Mesh (Fences, slabs, etc)
		}

		private bool IsTransparent(BlockState state)
		{
			if (state.IsEmpty || state.ID == 0) return true; // Air
			Block block = BlockPrototypes[state.ID];
			return block.IsTransparent || GetMeshType(state) == 3;
		}

		private void CreateGreedyQuad(Mask mask, int direction, int axis1, int axis2, int width, int height, int3 basePos)
		{
			NativeMesh mesh        = mask.MeshType == 1 ? SolidMesh : FluidMesh;
			Block      block       = BlockPrototypes[mask.BlockID];
			var        vertexCount = mesh.Vertices.Length;

			int normalIdx = direction switch
			                {
				                0 => mask.Normal > 0 ? 4 : 5,
				                1 => mask.Normal > 0 ? 3 : 2,
				                _ => mask.Normal > 0 ? 0 : 1
			                };

			float faceCoord = basePos[direction];

			var v1 = new float3();
			var v2 = new float3();
			var v3 = new float3();
			var v4 = new float3();

			v1[direction] = faceCoord;
			v2[direction] = faceCoord;
			v3[direction] = faceCoord;
			v4[direction] = faceCoord;

			v1[axis1] = basePos[axis1];
			v1[axis2] = basePos[axis2];

			v2[axis1] = basePos[axis1] + width;
			v2[axis2] = basePos[axis2];

			v3[axis1] = basePos[axis1];
			v3[axis2] = basePos[axis2] + height;

			v4[axis1] = basePos[axis1] + width;
			v4[axis2] = basePos[axis2] + height;

			float v_1, u2, v_2, u3, v_3, u4, v_4;

			float u1 = 0;
			v_1 = 0;
			if (direction is 0 or 1)
			{
				u2  = 0;
				v_2 = width;
				u3  = height;
				v_3 = 0;
				u4  = height;
				v_4 = width;
			}
			else
			{
				u2  = width;
				v_2 = 0;
				u3  = 0;
				v_3 = height;
				u4  = width;
				v_4 = height;
			}

			switch (direction)
			{
				// Flip UVs appropriately to prevent horizontal mirroring
				case 0 when mask.Normal < 0:
				case 1 when mask.Normal > 0:
					u1 = height - u1;
					u2 = height - u2;
					u3 = height - u3;
					u4 = height - u4;
					break;
				case 2 when mask.Normal < 0:
					u1 = width - u1;
					u2 = width - u2;
					u3 = width - u3;
					u4 = width - u4;
					break;
			}

			float3 tXYZ   = direction == 0 ? new float3(0, 0, 1) : new float3(1, 0, 0);
			var    tangent = new float4(tXYZ, 1f);

			mesh.Vertices.Add(PackVertex(v1, u1, v_1, block, normalIdx, mask.AO.x, tangent));
			mesh.Vertices.Add(PackVertex(v2, u2, v_2, block, normalIdx, mask.AO.y, tangent));
			mesh.Vertices.Add(PackVertex(v3, u3, v_3, block, normalIdx, mask.AO.z, tangent));
			mesh.Vertices.Add(PackVertex(v4, u4, v_4, block, normalIdx, mask.AO.w, tangent));

			if (mask.Normal > 0)
			{
				mesh.Triangles.Add(vertexCount);
				mesh.Triangles.Add(vertexCount + 2);
				mesh.Triangles.Add(vertexCount + 1);
				mesh.Triangles.Add(vertexCount + 1);
				mesh.Triangles.Add(vertexCount + 2);
				mesh.Triangles.Add(vertexCount + 3);
			}
			else
			{
				mesh.Triangles.Add(vertexCount);
				mesh.Triangles.Add(vertexCount + 1);
				mesh.Triangles.Add(vertexCount + 2);
				mesh.Triangles.Add(vertexCount + 1);
				mesh.Triangles.Add(vertexCount + 3);
				mesh.Triangles.Add(vertexCount + 2);
			}
		}

		private void RenderCustomMesh(int x, int y, int z, BlockState blockState)
		{
			Block               block    = BlockPrototypes[blockState.ID];
			NativeVoxelMeshData meshData = CustomMeshes[block.MeshID];
			quaternion          rot      = GetRotation(block.DirectionType, blockState.Orientation);
			var                 wPos     = new float3(x, y, z);

			for (var i = 0; i < meshData.Triangles.Length; i++)
			{
				int4   quad          = meshData.Triangles[i];
				float3 rotatedNormal = math.round(math.mul(rot, FaceChecks[i]));
				var    dir           = new int3(rotatedNormal);

				if (NeighbourHidesFace(x, y, z, dir, block.IsTransparent)) continue;

				float3 v0 = math.mul(rot, meshData.Vertices[quad.x] - 0.5f) + 0.5f + wPos;
				float3 v1 = math.mul(rot, meshData.Vertices[quad.y] - 0.5f) + 0.5f + wPos;
				float3 v2 = math.mul(rot, meshData.Vertices[quad.z] - 0.5f) + 0.5f + wPos;
				float3 v3 = math.mul(rot, meshData.Vertices[quad.w] - 0.5f) + 0.5f + wPos;

				float3 rotatedTangent = math.round(math.mul(rot, FaceTangents[i]));
				float3 originalBitangent = math.cross(FaceChecks[i], FaceTangents[i]);
				float3 expectedBitangent = math.cross(rotatedNormal, rotatedTangent);
				var tangentW = math.dot(math.round(math.mul(rot, originalBitangent)), expectedBitangent) >= 0f ? 1f : -1f;
				var tangent4 = new float4(rotatedTangent, tangentW);

				var normalIdx = (int)DirToIndex(rotatedNormal);

				NativeMesh targetMesh = block.IsFluid ? FluidMesh : SolidMesh;
				var        b          = targetMesh.Vertices.Length;

				targetMesh.Vertices.Add(PackVertex(v0, 0f, 0f, block, normalIdx, 3, tangent4));
				targetMesh.Vertices.Add(PackVertex(v1, 0f, 1f, block, normalIdx, 3, tangent4));
				targetMesh.Vertices.Add(PackVertex(v2, 1f, 0f, block, normalIdx, 3, tangent4));
				targetMesh.Vertices.Add(PackVertex(v3, 1f, 1f, block, normalIdx, 3, tangent4));

				targetMesh.Triangles.Add(b);
				targetMesh.Triangles.Add(b + 1);
				targetMesh.Triangles.Add(b + 3);
				targetMesh.Triangles.Add(b);
				targetMesh.Triangles.Add(b + 3);
				targetMesh.Triangles.Add(b + 2);
			}
		}

		private bool NeighbourHidesFace(int x, int y, int z, int3 dir, bool isTransparent)
		{
			BlockState nb = Accessor.GetBlockState(x + dir.x, y + dir.y, z + dir.z);
			if (nb.IsEmpty || nb.ID == 0) return false;
			return !(BlockPrototypes[nb.ID].IsTransparent && !isTransparent);
		}

		private static Vertex PackVertex(float3 pos, float u, float v, Block block, int normalIdx, int ao, float4 tangent)
		{
			var px       = (uint)math.round(math.clamp(pos.x * 10f, 0f, 1023f));
			var py       = (uint)math.round(math.clamp(pos.y * 10f, 0f, 1023f));
			var pz       = (uint)math.round(math.clamp(pos.z * 10f, 0f, 1023f));
			var aoPacked = (uint)ao & 0x3;

			var uPacked = (uint)math.round(math.clamp(u * 10f, 0f, 1023f));
			var vPacked = (uint)math.round(math.clamp(v * 10f, 0f, 1023f));

			var tanSign = tangent.w >= 0f ? 1u : 0u;
			var data1   = px | (py << 10) | (pz << 20) | (aoPacked << 30);

			Color32 color = block.TintColor;
			var     data2 = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

			uint tBase    = GetTextureIndex(normalIdx, block.BaseTextures);
			uint tOverlay = GetTextureIndex(normalIdx, block.OverlayTextures);
			uint tNorm    = GetTextureIndex(normalIdx, block.NormalTextures);
			uint tSpec    = GetTextureIndex(normalIdx, block.SpecularTextures);
			var  tanIdx   = DirToIndex(tangent.xyz);

			var data3 = (tBase & 0x1FFu) | ((tOverlay & 0x1FFu) << 9) | (uPacked << 18) | ((uint)normalIdx << 28) |
			            (tanSign << 31);
			var data4 = (tNorm & 0x1FFu) | ((tSpec & 0x1FFu) << 9) | (vPacked << 18) | (tanIdx << 28);

			return new Vertex { Data1 = data1, Data2 = data2, Data3 = data3, Data4 = data4 };
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

		private static ushort GetTextureIndex(int normalIdx, in NativeTexturesIDLayer layer)
		{
			return normalIdx switch
			       {
				       2 => layer.Top, 3  => layer.Bottom, 5 => layer.Right, 4 => layer.Left, 1 => layer.Front,
				       0 => layer.Back, _ => layer.Front
			       };
		}

		private static quaternion GetRotation(BlockDirectionType type, byte orientation)
		{
			return type switch
			       {
				       BlockDirectionType.YAxis => orientation switch
				                                   {
					                                   2 => quaternion.identity, 4 => quaternion.Euler(0, math.PI / 2f, 0),
					                                   3 => quaternion.Euler(0, math.PI, 0),
					                                   5 => quaternion.Euler(0, -math.PI / 2f, 0), _ => quaternion.identity
				                                   },
				       BlockDirectionType.AllAxes => orientation switch
				                                     {
					                                     0 => quaternion.identity, 1 => quaternion.Euler(math.PI, 0, 0),
					                                     2 => quaternion.Euler(math.PI / 2f, 0, 0),
					                                     3 => quaternion.Euler(-math.PI / 2f, 0, 0),
					                                     4 => quaternion.Euler(0, 0, -math.PI / 2f),
					                                     5 => quaternion.Euler(0, 0, math.PI / 2f), _ => quaternion.identity
				                                     },
				       _ => quaternion.identity
			       };
		}
		
		[BurstCompile]
		private static int4 ComputeAOMask(ChunkAccessor accessor, int3 pos, int3 coord, int axis1, int axis2)
		{
			var L = coord;
			var R = coord;
			var B = coord;
			var T = coord;

			var LBC = coord;
			var RBC = coord;
			var LTC = coord;
			var RTC = coord;

			L[axis2] -= 1;
			R[axis2] += 1;
			B[axis1] -= 1;
			T[axis1] += 1;

			LBC[axis1] -= 1;
			LBC[axis2] -= 1;
			RBC[axis1] -= 1;
			RBC[axis2] += 1;
			LTC[axis1] += 1;
			LTC[axis2] -= 1;
			RTC[axis1] += 1;
			RTC[axis2] += 1;

			var LO = GetMeshIndex(accessor.GetBlockInChunk(pos, L)) != 9 ? 1 : 0;
			var RO = GetMeshIndex(accessor.GetBlockInChunk(pos, R)) != 9 ? 1 : 0;
			var BO = GetMeshIndex(accessor.GetBlockInChunk(pos, B)) != 9 ? 1 : 0;
			var TO = GetMeshIndex(accessor.GetBlockInChunk(pos, T)) != 9 ? 1 : 0;

			var LBCO = GetMeshIndex(accessor.GetBlockInChunk(pos, LBC)) != 9 ? 1 : 0;
			var RBCO = GetMeshIndex(accessor.GetBlockInChunk(pos, RBC)) != 9 ? 1 : 0;
			var LTCO = GetMeshIndex(accessor.GetBlockInChunk(pos, LTC)) != 9 ? 1 : 0;
			var RTCO = GetMeshIndex(accessor.GetBlockInChunk(pos, RTC)) != 9 ? 1 : 0;

			return new int4(
			                ComputeAO(LO, BO, LBCO),
			                ComputeAO(LO, TO, LTCO),
			                ComputeAO(RO, BO, RBCO),
			                ComputeAO(RO, TO, RTCO)
			               );
		}
		
		private static int ComputeAO(int s1, int s2, int c)
		{
			if (s1 == 1 && s2 == 1)
			{
				return 0;
			}

			return 3 - (s1 + s2 + c);
		}
	}
}