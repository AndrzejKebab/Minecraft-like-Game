using System.Runtime.CompilerServices;
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
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	public unsafe struct GreedyMeshJob : IJobFor
	{
		[ReadOnly] public NativeArray<Entity>         Entities;
		[ReadOnly] public NativeArray<int3>           Positions;
		[ReadOnly] public NativeHashMap<int3, Entity> ChunkMap;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeHashMap<Entity, ChunkComponent> BlockDataLookup;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeArray<Block> BlockPrototypes;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeArray<NativeVoxelMeshData> MeshDatas;
		
		public EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int index)
		{
			Entity entity = Entities[index];
			int3   pos    = Positions[index];

			ChunkMap.TryGetValue(pos + new int3(0,  0, -1), out Entity nZNeg);
			ChunkMap.TryGetValue(pos + new int3(0,  0,  1), out Entity nZPos);
			ChunkMap.TryGetValue(pos + new int3(0, -1,  0), out Entity nYNeg);
			ChunkMap.TryGetValue(pos + new int3(0,  1,  0), out Entity nYPos);
			ChunkMap.TryGetValue(pos + new int3(-1, 0,  0), out Entity nXNeg);
			ChunkMap.TryGetValue(pos + new int3(1,  0,  0), out Entity nXPos);

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
			var totalI = solidIndices.Length  + fluidIndices.Length;

			var meshData = new ChunkMeshData
			{
				CombinedVertices = new NativeList<Vertex>(totalV, Allocator.Persistent),
				CombinedIndices  = new NativeList<int>(totalI,    Allocator.Persistent),
				SolidVertexCount = solidVertices.Length,
				SolidIndexCount  = solidIndices.Length
			};

			meshData.CombinedVertices.ResizeUninitialized(totalV);
			meshData.CombinedIndices.ResizeUninitialized(totalI);

			if (solidVertices.Length > 0)
			{
				UnsafeUtility.MemCpy(meshData.CombinedVertices.GetUnsafePtr(),
				                     solidVertices.GetUnsafePtr(),
				                     solidVertices.Length * UnsafeUtility.SizeOf<Vertex>());
				UnsafeUtility.MemCpy(meshData.CombinedIndices.GetUnsafePtr(),
				                     solidIndices.GetUnsafePtr(),
				                     solidIndices.Length * sizeof(int));
			}

			if (fluidVertices.Length > 0)
			{
				UnsafeUtility.MemCpy(meshData.CombinedVertices.GetUnsafePtr() + solidVertices.Length,
				                     fluidVertices.GetUnsafePtr(),
				                     fluidVertices.Length * UnsafeUtility.SizeOf<Vertex>());
				var  svCount  = solidVertices.Length;
				var  siCount  = solidIndices.Length;
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

		// ─────────────────────────────────────────────────────────────────────────
		// MESH GENERATION
		// ─────────────────────────────────────────────────────────────────────────

		[BurstCompile]
		private void GenerateMesh(ref ChunkAccessor   accessor,
		                          ref NativeList<Vertex> solidVertices, ref NativeList<int> solidIndices,
		                          ref NativeList<Vertex> fluidVertices, ref NativeList<int> fluidIndices)
		{
			var CS = accessor.ChunkSize; // 32

			// Two face maps, one per normal direction (front/back) for the current layer.
			// Size = CS*CS uints. Indexed [axis2 * CS + axis1].
			var faceMapFront = new NativeArray<uint>(CS * CS, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			var faceMapBack  = new NativeArray<uint>(CS * CS, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

			// ── 1. GREEDY MESHING ──────────────────────
			for (var direction = 0; direction < 3; direction++)
			{
				var axis1 = (direction + 1) % 3;
				var axis2 = (direction + 2) % 3;

				int3 chunkItr      = int3.zero;
				int3 directionMask = int3.zero;
				directionMask[direction] = 1;

				for (chunkItr[direction] = -1; chunkItr[direction] < CS;)
				{
					// Build face maps for this layer ──────────────────────────────
					var n = 0;
					for (chunkItr[axis2] = 0; chunkItr[axis2] < CS; chunkItr[axis2]++)
					for (chunkItr[axis1] = 0; chunkItr[axis1] < CS; chunkItr[axis1]++)
					{
						BlockState current = accessor.GetBlockState(chunkItr);
						BlockState compare = accessor.GetBlockState(chunkItr + directionMask);

						var currentType = GetMeshType(current);
						var compareType = GetMeshType(compare);

						var currentTransparent = IsTransparent(current);
						var compareTransparent = IsTransparent(compare);

						// Front face: current block facing the compare side
						uint mFront = 0;
						if (currentType != 0 && currentType != 3)
						{
							var faceVisible = compareTransparent;
							if (currentType == compareType && currentType == 2) faceVisible = false;
							if (faceVisible)
							{
								int4 ao = ComputeAOMask(ref accessor, chunkItr + directionMask, axis1, axis2);
								mFront  = PackMask(current.ID, currentType, current.Orientation, -1, ao);
							}
						}

						// Back face: compare block facing the current side
						uint mBack = 0;
						if (compareType != 0 && compareType != 3)
						{
							var faceVisible = currentTransparent;
							if (compareType == currentType && compareType == 2) faceVisible = false;
							if (faceVisible)
							{
								int4 ao = ComputeAOMask(ref accessor, chunkItr, axis1, axis2);
								mBack   = PackMask(compare.ID, compareType, compare.Orientation, 1, ao);
							}
						}

						faceMapFront[n] = mFront;
						faceMapBack[n]  = mBack;
						n++;
					}

					chunkItr[direction]++;

					// Binary greedy sweep on both face maps for this layer
					BinaryGreedySweep(faceMapFront, direction, axis1, axis2, CS, chunkItr[direction],
					                  ref solidVertices, ref solidIndices, ref fluidVertices, ref fluidIndices);
					BinaryGreedySweep(faceMapBack,  direction, axis1, axis2, CS, chunkItr[direction],
					                  ref solidVertices, ref solidIndices, ref fluidVertices, ref fluidIndices);
				}
			}

			faceMapFront.Dispose();
			faceMapBack.Dispose();
		}

		// ─────────────────────────────────────────────────────────────────────────
		// BINARY GREEDY SWEEP
		//
		// For CS=32 each axis row is exactly 32 blocks → fits in one uint bitmask.
		//
		// Algorithm:
		//   1. Group faces by packed mask value (blockID + meshType + AO + orientation
		//      + normal). Each group gets a uint[CS] row-bitmask array where bit x of
		//      row y is set iff faceMap[y*CS+x] == that mask value.
		//
		//   2. For each group, sweep rows using:
		//      - tzcnt(remaining) → first unvisited face in O(1)
		//      - tzcnt(~(remaining>>x)) → contiguous run width in O(1)
		//      - (rowBits[y+h] & ~visited[y+h] & lineMask) == lineMask → height
		//        check in O(1) per row (vs O(w) scalar scan in old code)
		//
		//   3. Single visited[] array shared across all groups — safe because each
		//      (x,y) position belongs to at most one group.
		// ─────────────────────────────────────────────────────────────────────────
		[BurstCompile]
		private void BinaryGreedySweep(
			NativeArray<uint>      faceMap,
			int direction, int axis1, int axis2, int CS, int layerCoord,
			ref NativeList<Vertex> solidV, ref NativeList<int> solidI,
			ref NativeList<Vertex> fluidV, ref NativeList<int> fluidI)
		{
			// ── Build per-group row bitmasks ──────────────────────────────────────
			// maskToGroup : packed mask value → group index
			// groupRowBits: flat array, groupRowBits[gIdx * CS + row] = uint bitmask
			//               where bit x is set if faceMap[row*CS+x] == that mask.
			var maskToGroup  = new NativeHashMap<uint, int>(32, Allocator.Temp);
			var groupRowBits = new NativeList<uint>(32 * CS, Allocator.Temp); // grows as groups are added
			var groupCount   = 0;

			for (var y = 0; y < CS; y++)
			for (var x = 0; x < CS; x++)
			{
				var m = faceMap[y * CS + x];
				if (m == 0) continue;

				if (!maskToGroup.TryGetValue(m, out var gIdx))
				{
					gIdx = groupCount++;
					maskToGroup.Add(m, gIdx);

					// Append CS zeroed uints for this group's row bitmasks.
					var oldLen = groupRowBits.Length;
					groupRowBits.ResizeUninitialized(oldLen + CS);
					UnsafeUtility.MemClear(
						groupRowBits.GetUnsafePtr() + oldLen,
						CS * sizeof(uint));
				}

				// Set bit x in this group's row y.
				// Re-fetch ptr after any potential realloc from ResizeUninitialized.
				groupRowBits.GetUnsafePtr()[gIdx * CS + y] |= 1u << x;
			}

			if (groupCount == 0)
			{
				maskToGroup.Dispose();
				groupRowBits.Dispose();
				return;
			}

			// ── Sweep ─────────────────────────────────────────────────────────────
			// One visited[] array for all groups — safe because positions are unique
			// per group (each cell has exactly one non-zero packed mask value).
			var  visitedArr   = new NativeArray<uint>(CS, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			var visited     = (uint*)visitedArr.GetUnsafePtr();
			var rowBitsBase = groupRowBits.GetUnsafePtr();

			NativeArray<uint> keys = maskToGroup.GetKeyArray(Allocator.Temp);

			for (var gi = 0; gi < keys.Length; gi++)
			{
				var m    = keys[gi];
				var  gIdx = maskToGroup[m];

				UnpackMask(m, out var blockID, out var meshType,
				           out var orientation, out var normal, out int4 ao);
				var isFluid = meshType == 2;

				var rowBits = rowBitsBase + gIdx * CS;

				for (var y = 0; y < CS; y++)
				{
					// Unvisited faces in this group on this row
					var remaining = rowBits[y] & ~visited[y];
					while (remaining != 0)
					{
						// ── Width (O(1)) ─────────────────────────────────────────
						// x = leftmost unvisited face
						var x = math.tzcnt(remaining);

						// Shift so x lands at bit 0, then count leading 1-run.
						// Using `remaining >> x` (not rowBits) ensures we don't
						// extend into already-visited positions that happen to have
						// the same mask value.
						var shifted = remaining >> x;
						var  w       = math.tzcnt(~shifted); // tzcnt(0) == 32 ✓

						// Build bitmask covering [x, x+w)
						// Special-case w==32: (1u<<32) wraps to 1 in C#, so guard.
						var lineMask = w < 32 ? ((1u << w) - 1u) << x : ~0u;

						// ── Height (O(1) per row) ────────────────────────────────
						// Each row check is one AND + compare, no scalar loop.
						var h = 1;
						while (y + h < CS &&
						       (rowBits[y + h] & ~visited[y + h] & lineMask) == lineMask)
							h++;

						// ── Mark visited ─────────────────────────────────────────
						for (var dy = 0; dy < h; dy++)
							visited[y + dy] |= lineMask;

						// ── Emit quad ────────────────────────────────────────────
						int3 basePos = int3.zero;
						basePos[direction] = layerCoord;
						basePos[axis1]     = x;
						basePos[axis2]     = y;

						if (isFluid)
							CreateGreedyQuad(blockID, orientation, normal, ao,
							                 direction, axis1, axis2, w, h, basePos,
							                 ref fluidV, ref fluidI);
						else
							CreateGreedyQuad(blockID, orientation, normal, ao,
							                 direction, axis1, axis2, w, h, basePos,
							                 ref solidV, ref solidI);

						// Recompute remaining after marking visited
						remaining = rowBits[y] & ~visited[y];
					}
				}
			}

			keys.Dispose();
			visitedArr.Dispose();
			maskToGroup.Dispose();
			groupRowBits.Dispose();
		}

		// ─────────────────────────────────────────────────────────────────────────
		// PACKING / UNPACKING
		// ─────────────────────────────────────────────────────────────────────────

		// Bit layout (32 bits total):
		//  [1:0]   meshType    (2 bits,  0-3)
		//  [17:2]  blockID     (16 bits, 0-65535)
		//  [20:18] orientation (3 bits,  0-7)
		//  [21]    normal sign (1 bit,   0=negative, 1=positive)
		//  [23:22] ao.x        (2 bits)
		//  [25:24] ao.y        (2 bits)
		//  [27:26] ao.z        (2 bits)
		//  [29:28] ao.w        (2 bits)

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint PackMask(ushort blockID, byte meshType, byte orientation, sbyte normal, int4 ao)
		{
			uint packed  = meshType;
			packed      |= (uint)blockID     << 2;
			packed      |= (uint)orientation << 18;
			packed      |= (normal > 0 ? 1u : 0u) << 21;
			packed      |= ((uint)ao.x & 3u) << 22;
			packed      |= ((uint)ao.y & 3u) << 24;
			packed      |= ((uint)ao.z & 3u) << 26;
			packed      |= ((uint)ao.w & 3u) << 28;
			return packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void UnpackMask(uint packed,
		                               out ushort blockID, out byte meshType,
		                               out byte orientation, out sbyte normal, out int4 ao)
		{
			meshType    = (byte)(packed         & 3u);
			blockID     = (ushort)((packed >> 2) & 0xFFFFu);
			orientation = (byte)((packed >> 18)  & 7u);
			normal      = (packed & (1u << 21)) != 0 ? (sbyte)1 : (sbyte)-1;
			ao          = new int4(
				(int)((packed >> 22) & 3u),
				(int)((packed >> 24) & 3u),
				(int)((packed >> 26) & 3u),
				(int)((packed >> 28) & 3u));
		}

		// ─────────────────────────────────────────────────────────────────────────
		// BLOCK QUERIES
		// ─────────────────────────────────────────────────────────────────────────

		private byte GetMeshType(BlockState state)
		{
			if (state.IsEmpty || state.ID == 0) return 0; // air
			Block block = BlockPrototypes[state.ID];
			if (block.IsFluid) return 2;                  // fluid
			return block.MeshID == 0 ? (byte)1 : (byte)3; // standard / custom
		}

		private bool IsTransparent(BlockState state)
		{
			if (state.IsEmpty || state.ID == 0) return true;
			Block block = BlockPrototypes[state.ID];
			return block.IsTransparent || GetMeshType(state) == 3;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// QUAD EMISSION
		// ─────────────────────────────────────────────────────────────────────────

		private void CreateGreedyQuad(
			ushort blockID, byte orientation, sbyte normal, int4 ao,
			int direction, int axis1, int axis2, int width, int height, int3 basePos,
			ref NativeList<Vertex> outVerts, ref NativeList<int> outTris)
		{
			Block block       = BlockPrototypes[blockID];
			var   vertexCount = outVerts.Length;

			var normalIdx = direction switch
			                {
				                0 => normal > 0 ? 4 : 5,
				                1 => normal > 0 ? 3 : 2,
				                _ => normal > 0 ? 0 : 1
			                };

			var textureFaceIdx = RemapTextureFace(normalIdx, block.DirectionType, orientation);
			float faceCoord    = basePos[direction];

			float3 v1 = float3.zero, v2 = float3.zero, v3 = float3.zero, v4 = float3.zero;

			v1[direction] = v2[direction] = v3[direction] = v4[direction] = faceCoord;
			v1[axis1] = basePos[axis1];           v1[axis2] = basePos[axis2];
			v2[axis1] = basePos[axis1] + width;   v2[axis2] = basePos[axis2];
			v3[axis1] = basePos[axis1];           v3[axis2] = basePos[axis2] + height;
			v4[axis1] = basePos[axis1] + width;   v4[axis2] = basePos[axis2] + height;

			outVerts.Add(new Vertex(v1, block, normalIdx, textureFaceIdx, ao.x));
			outVerts.Add(new Vertex(v2, block, normalIdx, textureFaceIdx, ao.y));
			outVerts.Add(new Vertex(v3, block, normalIdx, textureFaceIdx, ao.z));
			outVerts.Add(new Vertex(v4, block, normalIdx, textureFaceIdx, ao.w));

			if (normal > 0)
			{
				outTris.Add(vertexCount);     outTris.Add(vertexCount + 2); outTris.Add(vertexCount + 1);
				outTris.Add(vertexCount + 1); outTris.Add(vertexCount + 2); outTris.Add(vertexCount + 3);
			}
			else
			{
				outTris.Add(vertexCount);     outTris.Add(vertexCount + 1); outTris.Add(vertexCount + 2);
				outTris.Add(vertexCount + 1); outTris.Add(vertexCount + 3); outTris.Add(vertexCount + 2);
			}
		}

		// ─────────────────────────────────────────────────────────────────────────
		// TEXTURE REMAPPING / ROTATION
		// ─────────────────────────────────────────────────────────────────────────

		private static int RemapTextureFace(int normalIdx, BlockDirectionType dirType, byte orientation)
		{
			return dirType switch
			{
				BlockDirectionType.None => normalIdx,
				BlockDirectionType.YAxis when normalIdx is 2 or 3 => normalIdx,
				BlockDirectionType.YAxis => orientation switch
				{
					2 => normalIdx,
					3 => normalIdx switch { 0 => 1, 1 => 0, 4 => 5, 5 => 4, _ => normalIdx },
					4 => normalIdx switch { 5 => 1, 4 => 0, 0 => 5, 1 => 4, _ => normalIdx },
					5 => normalIdx switch { 4 => 1, 5 => 0, 1 => 5, 0 => 4, _ => normalIdx },
					_ => normalIdx
				},
				BlockDirectionType.AllAxes => orientation switch
				{
					0 => normalIdx,
					1 => normalIdx switch { 2 => 3, 3 => 2, 1 => 0, 0 => 1, _ => normalIdx },
					2 => normalIdx switch { 1 => 2, 3 => 1, 0 => 3, 2 => 0, _ => normalIdx },
					3 => normalIdx switch { 0 => 2, 3 => 0, 1 => 3, 2 => 1, _ => normalIdx },
					4 => normalIdx switch { 5 => 2, 3 => 5, 4 => 3, 2 => 4, _ => normalIdx },
					5 => normalIdx switch { 4 => 2, 3 => 4, 5 => 3, 2 => 5, _ => normalIdx },
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
					2 => quaternion.identity,
					4 => quaternion.Euler(0,  math.PI / 2f, 0),
					3 => quaternion.Euler(0,  math.PI,      0),
					5 => quaternion.Euler(0, -math.PI / 2f, 0),
					_ => quaternion.identity
				},
				BlockDirectionType.AllAxes => orientation switch
				{
					0 => quaternion.identity,
					1 => quaternion.Euler(math.PI,        0, 0),
					2 => quaternion.Euler(math.PI / 2f,   0, 0),
					3 => quaternion.Euler(-math.PI / 2f,  0, 0),
					4 => quaternion.Euler(0, 0, -math.PI / 2f),
					5 => quaternion.Euler(0, 0,  math.PI / 2f),
					_ => quaternion.identity
				},
				_ => quaternion.identity
			};
		}

		// ─────────────────────────────────────────────────────────────────────────
		// AMBIENT OCCLUSION
		// ─────────────────────────────────────────────────────────────────────────

		private int4 ComputeAOMask(ref ChunkAccessor accessor, int3 airPos, int axis1, int axis2)
		{
			int3 l   = airPos; l[axis1]   -= 1;
			int3 r   = airPos; r[axis1]   += 1;
			int3 b   = airPos; b[axis2]   -= 1;
			int3 T   = airPos; T[axis2]   += 1;
			int3 lbc = airPos; lbc[axis1] -= 1; lbc[axis2] -= 1;
			int3 rbc = airPos; rbc[axis1] += 1; rbc[axis2] -= 1;
			int3 ltc = airPos; ltc[axis1] -= 1; ltc[axis2] += 1;
			int3 rtc = airPos; rtc[axis1] += 1; rtc[axis2] += 1;

			var lo   = IsOpaque(ref accessor, l)   ? 1 : 0;
			var ro   = IsOpaque(ref accessor, r)   ? 1 : 0;
			var bo   = IsOpaque(ref accessor, b)   ? 1 : 0;
			var to   = IsOpaque(ref accessor, T)   ? 1 : 0;
			var lbco = IsOpaque(ref accessor, lbc) ? 1 : 0;
			var rbco = IsOpaque(ref accessor, rbc) ? 1 : 0;
			var ltco = IsOpaque(ref accessor, ltc) ? 1 : 0;
			var rtco = IsOpaque(ref accessor, rtc) ? 1 : 0;

			return new int4(
				ComputeAO(lo, bo, lbco),
				ComputeAO(ro, bo, rbco),
				ComputeAO(lo, to, ltco),
				ComputeAO(ro, to, rtco));
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