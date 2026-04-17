using _Project.Tags;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Jobs
{[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public unsafe struct GreedyMeshJob : IJobFor
	{
		[ReadOnly] public NativeArray<Entity>         Entities;
		[ReadOnly] public NativeArray<int3>           Positions;
		[ReadOnly] public NativeHashMap<int3, Entity> ChunkMap;

		[NativeDisableContainerSafetyRestriction] [ReadOnly]
		public NativeHashMap<Entity, ChunkComponent> BlockDataLookup;

		[NativeDisableContainerSafetyRestriction] [ReadOnly]
		public NativeArray<Block> BlockPrototypes;

		[NativeDisableContainerSafetyRestriction] [ReadOnly]
		public NativeArray<NativeVoxelMeshData> CustomMeshes;

		[ReadOnly] public NativeArray<int3> FaceChecks;

		public EntityCommandBuffer.ParallelWriter ECB;

		private struct Mask
		{
			public ushort BlockID;
			public byte   MeshType;
			public byte   Orientation;
			public sbyte  Normal;
			public int4   AO;
		}

		public void Execute(int index)
		{
			Entity entity = Entities[index];
			int3   pos    = Positions[index];

			ChunkMap.TryGetValue(pos + new int3(0, 0, -1), out Entity nZNeg);
			ChunkMap.TryGetValue(pos + new int3(0, 0, 1), out Entity nZPos);
			ChunkMap.TryGetValue(pos + new int3(0, -1, 0), out Entity nYNeg);
			ChunkMap.TryGetValue(pos + new int3(0, 1, 0), out Entity nYPos);
			ChunkMap.TryGetValue(pos + new int3(-1, 0, 0), out Entity nXNeg);
			ChunkMap.TryGetValue(pos + new int3(1, 0, 0), out Entity nXPos);

			var accessor = new ChunkAccessor
			               {
				               Center       = BlockDataLookup[entity].BlockData,
				               NeighborZNeg = BlockDataLookup[nZNeg].BlockData,
				               NeighborZPos = BlockDataLookup[nZPos].BlockData,
				               NeighborYNeg = BlockDataLookup[nYNeg].BlockData,
				               NeighborYPos = BlockDataLookup[nYPos].BlockData,
				               NeighborXNeg = BlockDataLookup[nXNeg].BlockData,
				               NeighborXPos = BlockDataLookup[nXPos].BlockData,
				               ChunkSize    = ChunkData.CHUNK_SIZE
			               };

			var solidVertices = new NativeList<Vertex>(Allocator.Temp);
			var solidIndices  = new NativeList<int>(Allocator.Temp);
			var fluidVertices = new NativeList<Vertex>(Allocator.Temp);
			var fluidIndices  = new NativeList<int>(Allocator.Temp);

			GenerateMesh(ref accessor, ref solidVertices, ref solidIndices, ref fluidVertices, ref fluidIndices);

			var totalV = solidVertices.Length + fluidVertices.Length;
			var totalI = solidIndices.Length + fluidIndices.Length;

			var meshData = new ChunkMeshData
			               {
				               CombinedVertices = new NativeList<Vertex>(totalV, Allocator.Persistent),
				               CombinedIndices  = new NativeList<int>(totalI, Allocator.Persistent),
				               SolidVertexCount = solidVertices.Length,
				               SolidIndexCount  = solidIndices.Length
			               };

			meshData.CombinedVertices.ResizeUninitialized(totalV);
			meshData.CombinedIndices.ResizeUninitialized(totalI);

			if (solidVertices.Length > 0)
			{
				UnsafeUtility.MemCpy(meshData.CombinedVertices.GetUnsafePtr(), solidVertices.GetUnsafePtr(),
				                     solidVertices.Length * UnsafeUtility.SizeOf<Vertex>());
				UnsafeUtility.MemCpy(meshData.CombinedIndices.GetUnsafePtr(), solidIndices.GetUnsafePtr(),
				                     solidIndices.Length * sizeof(int));
			}

			if (fluidVertices.Length > 0)
			{
				UnsafeUtility.MemCpy(meshData.CombinedVertices.GetUnsafePtr() + solidVertices.Length,
				                     fluidVertices.GetUnsafePtr(), fluidVertices.Length * UnsafeUtility.SizeOf<Vertex>());
				var svCount  = solidVertices.Length;
				var siCount  = solidIndices.Length;
				var fIndices = fluidIndices.GetUnsafePtr();
				var cIndices = meshData.CombinedIndices.GetUnsafePtr();
				for (var i = 0; i < fluidIndices.Length; i++)
					cIndices[siCount + i] = fIndices[i] + svCount;
			}

			ECB.AddComponent(index, entity, meshData);

			if (totalV > 0)
				ECB.AddComponent<MeshRequiresUpload>(index, entity);

			solidVertices.Dispose();
			solidIndices.Dispose();
			fluidVertices.Dispose();
			fluidIndices.Dispose();
		}

		[BurstCompile]
		private void GenerateMesh(ref ChunkAccessor   accessor,     ref NativeList<Vertex> solidVertices,
		                          ref NativeList<int> solidIndices, ref NativeList<Vertex> fluidVertices,
		                          ref NativeList<int> fluidIndices)
		{
			var chunkSize = accessor.ChunkSize;
			var maskFront =
				new NativeArray<Mask>(chunkSize * chunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			var maskBack =
				new NativeArray<Mask>(chunkSize * chunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
						BlockState current = accessor.GetBlockState(chunkItr);
						BlockState compare = accessor.GetBlockState(chunkItr + directionMask);

						var currentType = GetMeshType(current);
						var compareType = GetMeshType(compare);

						var currentTransparent = IsTransparent(current);
						var compareTransparent = IsTransparent(compare);

						// Face of current block (facing positive)
						if (currentType != 0 && currentType != 3)
						{
							var faceVisible                                                 = compareTransparent;
							if (currentType == compareType && currentType == 2) faceVisible = false;

							if (faceVisible)
							{
								mFront.BlockID     = current.ID;
								mFront.Orientation = current.Orientation;
								mFront.MeshType    = currentType;
								mFront.Normal      = -1;
								mFront.AO          = ComputeAOMask(ref accessor, chunkItr + directionMask, axis1, axis2);
							}
							else
							{
								mFront.MeshType = 0;
							}
						}
						else
						{
							mFront.MeshType = 0;
						}

						// Face of compare block (facing negative)
						if (compareType != 0 && compareType != 3)
						{
							var faceVisible                                                 = currentTransparent;
							if (compareType == currentType && compareType == 2) faceVisible = false;

							if (faceVisible)
							{
								mBack.BlockID     = compare.ID;
								mBack.Orientation = compare.Orientation;
								mBack.MeshType    = compareType;
								mBack.Normal      = 1;
								mBack.AO          = ComputeAOMask(ref accessor, chunkItr, axis1, axis2);
							}
							else
							{
								mBack.MeshType = 0;
							}
						}
						else
						{
							mBack.MeshType = 0;
						}

						maskFront[n] = mFront;
						maskBack[n]  = mBack;
						n++;
					}

					chunkItr[direction]++;

					ProcessMask(maskFront, direction, axis1, axis2, chunkSize, chunkItr, ref solidVertices,
					            ref solidIndices, ref fluidVertices, ref fluidIndices);
					ProcessMask(maskBack, direction, axis1, axis2, chunkSize, chunkItr, ref solidVertices, ref solidIndices,
					            ref fluidVertices, ref fluidIndices);
				}
			}

			maskFront.Dispose();
			maskBack.Dispose();

			// 2. CUSTOM MESHING (Slabs, Fences, Foliage)
			for (var x = 0; x < chunkSize; x++)
			for (var y = 0; y < chunkSize; y++)
			for (var z = 0; z < chunkSize; z++)
			{
				BlockState state = accessor.GetBlockState(x, y, z);
				if (state.IsEmpty || GetMeshType(state) != 3) continue;

				RenderCustomMesh(ref accessor, x, y, z, state, ref solidVertices, ref solidIndices, ref fluidVertices,
				                 ref fluidIndices);
			}
		}

		private void ProcessMask(NativeArray<Mask> mask, int direction, int axis1, int axis2, int chunkSize, int3 chunkItr,
		                         ref NativeList<Vertex> solidV, ref NativeList<int> solidI, ref NativeList<Vertex> fluidV,
		                         ref NativeList<int> fluidI)
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

					if (currentMask.MeshType == 2)
						CreateGreedyQuad(currentMask, direction, axis1, axis2, width, height, basePos, ref fluidV,
						                 ref fluidI);
					else
						CreateGreedyQuad(currentMask, direction, axis1, axis2, width, height, basePos, ref solidV,
						                 ref solidI);

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
			return a.MeshType == b.MeshType && a.BlockID == b.BlockID && a.Normal == b.Normal
			       && a.AO.Equals(b.AO) && a.Orientation == b.Orientation;
		}

		private byte GetMeshType(BlockState state)
		{
			if (state.IsEmpty || state.ID == 0) return 0; // Air
			Block block = BlockPrototypes[state.ID];
			if (block.IsFluid) return 2; // Fluid
			return block.MeshID == 0 ? (byte)1 : (byte)3;
		}

		private bool IsTransparent(BlockState state)
		{
			if (state.IsEmpty || state.ID == 0) return true; // Air
			Block block = BlockPrototypes[state.ID];
			return block.IsTransparent || GetMeshType(state) == 3;
		}

		private void CreateGreedyQuad(Mask mask, int direction, int axis1, int axis2, int width, int height, int3 basePos,
		                              ref NativeList<Vertex> outVerts, ref NativeList<int> outTris)
		{
			Block block       = BlockPrototypes[mask.BlockID];
			var   vertexCount = outVerts.Length;

			var normalIdx = direction switch
			                {
				                0 => mask.Normal > 0 ? 4 : 5,
				                1 => mask.Normal > 0 ? 3 : 2,
				                _ => mask.Normal > 0 ? 0 : 1
			                };

			var textureFaceIdx = RemapTextureFace(normalIdx, block.DirectionType, mask.Orientation);

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

			outVerts.Add(new Vertex(v1, block, normalIdx, textureFaceIdx, mask.AO.x));
			outVerts.Add(new Vertex(v2, block, normalIdx, textureFaceIdx, mask.AO.y));
			outVerts.Add(new Vertex(v3, block, normalIdx, textureFaceIdx, mask.AO.z));
			outVerts.Add(new Vertex(v4, block, normalIdx, textureFaceIdx, mask.AO.w));

			if (mask.Normal > 0)
			{
				outTris.Add(vertexCount);
				outTris.Add(vertexCount + 2);
				outTris.Add(vertexCount + 1);
				outTris.Add(vertexCount + 1);
				outTris.Add(vertexCount + 2);
				outTris.Add(vertexCount + 3);
			}
			else
			{
				outTris.Add(vertexCount);
				outTris.Add(vertexCount + 1);
				outTris.Add(vertexCount + 2);
				outTris.Add(vertexCount + 1);
				outTris.Add(vertexCount + 3);
				outTris.Add(vertexCount + 2);
			}
		}

		private void RenderCustomMesh(ref ChunkAccessor      accessor, int x, int y, int z, BlockState blockState,
		                              ref NativeList<Vertex> solidV,   ref NativeList<int> solidI,
		                              ref NativeList<Vertex> fluidV,   ref NativeList<int> fluidI)
		{
			Block               block    = BlockPrototypes[blockState.ID];
			NativeVoxelMeshData meshData = CustomMeshes[block.MeshID];
			quaternion          rot      = GetRotation(block.DirectionType, blockState.Orientation);
			var                 wPos     = new float3(x, y, z);

			ref NativeList<Vertex> targetV = ref block.IsFluid ? ref fluidV : ref solidV;
			ref NativeList<int>    targetI = ref block.IsFluid ? ref fluidI : ref solidI;

			for (var i = 0; i < meshData.Triangles.Length; i++)
			{
				int4   quad          = meshData.Triangles[i];
				float3 rotatedNormal = math.round(math.mul(rot, FaceChecks[i]));
				var    dir           = new int3(rotatedNormal);

				if (NeighbourHidesFace(ref accessor, x, y, z, dir, block.IsTransparent)) continue;

				float3 v0 = math.mul(rot, meshData.Vertices[quad.x] - 0.5f) + 0.5f + wPos;
				float3 v1 = math.mul(rot, meshData.Vertices[quad.y] - 0.5f) + 0.5f + wPos;
				float3 v2 = math.mul(rot, meshData.Vertices[quad.z] - 0.5f) + 0.5f + wPos;
				float3 v3 = math.mul(rot, meshData.Vertices[quad.w] - 0.5f) + 0.5f + wPos;

				var normalIdx = (int)DirToIndex(rotatedNormal);
				var b         = targetV.Length;

				targetV.Add(new Vertex(v0, block, normalIdx, 3));
				targetV.Add(new Vertex(v1, block, normalIdx, 3));
				targetV.Add(new Vertex(v2, block, normalIdx, 3));
				targetV.Add(new Vertex(v3, block, normalIdx, 3));

				targetI.Add(b);
				targetI.Add(b + 1);
				targetI.Add(b + 3);
				targetI.Add(b);
				targetI.Add(b + 3);
				targetI.Add(b + 2);
			}
		}

		private bool NeighbourHidesFace(ref ChunkAccessor accessor, int x, int y, int z, int3 dir, bool isTransparent)
		{
			BlockState nb = accessor.GetBlockState(x + dir.x, y + dir.y, z + dir.z);
			if (nb.IsEmpty || nb.ID == 0) return false;
			return !(BlockPrototypes[nb.ID].IsTransparent && !isTransparent);
		}

		private static uint DirToIndex(float3 dir)
		{
			return dir.z switch
			       {
				       < -0.5f => 0, > 0.5f => 1,
				       _ => dir.y switch
				            {
					            > 0.5f => 2, < -0.5f => 3,
					            _      => dir.x < -0.5f ? 4 : (uint)5
				            }
			       };
		}

		/// <summary>
		///     Maps a geometric face index (0-5) to the logical texture-slot face index, accounting for
		///     block orientation. Top (2) and Bottom (3) are never remapped for YAxis blocks.
		///     Face index conventions (matches DirToIndex / GetTextureIndex):
		///     0 = Back  (-Z)   1 = Front (+Z)   2 = Top   (+Y)
		///     3 = Bottom(-Y)   4 = Left  (-X)   5 = Right (+X)
		///     YAxis orientation encoding (matches PlayerInteractionSystem assignment):
		///     2 = identity (block front → +Z)   3 = 180° Y (block front → -Z)
		///     4 = +90° Y   (block front → +X)   5 = -90° Y (block front → -X)
		/// </summary>
		private static int RemapTextureFace(int normalIdx, BlockDirectionType dirType, byte orientation)
		{
			return dirType switch
			       {
				       BlockDirectionType.None                           => normalIdx,
				       BlockDirectionType.YAxis when normalIdx is 2 or 3 => normalIdx // Top/Bottom unchanged
				       ,
				       BlockDirectionType.YAxis => orientation switch
				                                   {
					                                   // identity: no remap
					                                   2 => normalIdx,
					                                   // 180° Y: front↔back, left↔right
					                                   3 => normalIdx switch
					                                        {
						                                        0 => 1,
						                                        1 => 0,
						                                        4 => 5,
						                                        5 => 4,
						                                        _ => normalIdx
					                                        },
					                                   // +90° Y: +X→Front, -X→Back, -Z→Right, +Z→Left
					                                   4 => normalIdx switch
					                                        {
						                                        5 => 1,
						                                        4 => 0,
						                                        0 => 5,
						                                        1 => 4,
						                                        _ => normalIdx
					                                        },
					                                   // -90° Y: -X→Front, +X→Back, +Z→Right, -Z→Left
					                                   5 => normalIdx switch
					                                        {
						                                        4 => 1,
						                                        5 => 0,
						                                        1 => 5,
						                                        0 => 4,
						                                        _ => normalIdx
					                                        },
					                                   _ => normalIdx
				                                   },
				       // AllAxes orientations (matches GetRotation):
				       // 0=identity, 1=180°X, 2=+90°X, 3=-90°X, 4=-90°Z, 5=+90°Z
				       BlockDirectionType.AllAxes => orientation switch
				                                     {
					                                     0 => normalIdx,
					                                     // 180° X: top↔bottom, front↔back
					                                     1 => normalIdx switch
					                                          {
						                                          2 => 3,
						                                          3 => 2,
						                                          1 => 0,
						                                          0 => 1,
						                                          _ => normalIdx
					                                          },
					                                     // +90° X: global front shows local top, global bottom shows local front, global back shows local bottom, global top shows local back
					                                     2 => normalIdx switch
					                                          {
						                                          1 => 2,
						                                          3 => 1,
						                                          0 => 3,
						                                          2 => 0,
						                                          _ => normalIdx
					                                          },
					                                     // -90° X: global back shows local top, global bottom shows local back, global front shows local bottom, global top shows local front
					                                     3 => normalIdx switch
					                                          {
						                                          0 => 2,
						                                          3 => 0,
						                                          1 => 3,
						                                          2 => 1,
						                                          _ => normalIdx
					                                          },
					                                     // -90° Z: global right shows local top, global bottom shows local right, global left shows local bottom, global top shows local left
					                                     4 => normalIdx switch
					                                          {
						                                          5 => 2,
						                                          3 => 5,
						                                          4 => 3,
						                                          2 => 4,
						                                          _ => normalIdx
					                                          },
					                                     // +90° Z: global left shows local top, global bottom shows local left, global right shows local bottom, global top shows local right
					                                     5 => normalIdx switch
					                                          {
						                                          4 => 2,
						                                          3 => 4,
						                                          5 => 3,
						                                          2 => 5,
						                                          _ => normalIdx
					                                          },
					                                     _ => normalIdx
				                                     },
				       _ => normalIdx
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

		private int4 ComputeAOMask(ref ChunkAccessor accessor, int3 airPos, int axis1, int axis2)
		{
			int3 l = airPos;
			l[axis1] -= 1;
			int3 r = airPos;
			r[axis1] += 1;
			int3 b = airPos;
			b[axis2] -= 1;
			int3 T = airPos;
			T[axis2] += 1;

			int3 lbc = airPos;
			lbc[axis1] -= 1;
			lbc[axis2] -= 1;
			int3 rbc = airPos;
			rbc[axis1] += 1;
			rbc[axis2] -= 1;
			int3 ltc = airPos;
			ltc[axis1] -= 1;
			ltc[axis2] += 1;
			int3 rtc = airPos;
			rtc[axis1] += 1;
			rtc[axis2] += 1;

			var lo = IsOpaque(ref accessor, l) ? 1 : 0;
			var ro = IsOpaque(ref accessor, r) ? 1 : 0;
			var bo = IsOpaque(ref accessor, b) ? 1 : 0;
			var to = IsOpaque(ref accessor, T) ? 1 : 0;

			var lbco = IsOpaque(ref accessor, lbc) ? 1 : 0;
			var rbco = IsOpaque(ref accessor, rbc) ? 1 : 0;
			var ltco = IsOpaque(ref accessor, ltc) ? 1 : 0;
			var rtco = IsOpaque(ref accessor, rtc) ? 1 : 0;

			return new int4(
			                ComputeAO(lo, bo, lbco),
			                ComputeAO(ro, bo, rbco),
			                ComputeAO(lo, to, ltco),
			                ComputeAO(ro, to, rtco)
			               );
		}

		private bool IsOpaque(ref ChunkAccessor accessor, int3 pos)
		{
			BlockState state = accessor.GetBlockState(pos);
			if (state.IsEmpty || state.ID == 0) return false;
			return !BlockPrototypes[state.ID].IsTransparent;
		}

		private static int ComputeAO(int side1, int side2, int corner)
		{
			if (side1 == 1 && side2 == 1) return 0;
			return 3 - (side1 + side2 + corner);
		}
	}
}
