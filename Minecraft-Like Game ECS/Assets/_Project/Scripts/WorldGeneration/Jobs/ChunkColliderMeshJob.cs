using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Material = Unity.Physics.Material;

namespace _Project.WorldGeneration.Jobs
{
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public struct ChunkColliderMeshJob : IJobFor
	{
		[ReadOnly] public NativeArray<Entity> Entities;
		[ReadOnly] public NativeArray<int3>   Positions;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeHashMap<Entity, ChunkComponent> BlockDataLookup;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeHashMap<int3, Entity> ChunkMap;

		[NativeDisableContainerSafetyRestriction] 
		[ReadOnly] public NativeArray<Block> BlockPrototypes;

		[NativeDisableParallelForRestriction] 
		[WriteOnly] public NativeArray<BlobAssetReference<Collider>> OutColliders;
		
		public CollisionFilter Filter;
		public int             ChunkSize;

		public void Execute(int index)
		{
			Entity entity = Entities[index];
			int3   pos    = Positions[index];

			if (!BlockDataLookup.TryGetValue(entity, out ChunkComponent ownComp))
			{
				OutColliders[index] = default;
				return;
			}

			NativeArray<BlockState> center = ownComp.BlockData;

			NativeArray<BlockState> nXNeg = TryGetNeighbor(pos + new int3(-1, 0, 0));
			NativeArray<BlockState> nXPos = TryGetNeighbor(pos + new int3(1, 0, 0));
			NativeArray<BlockState> nYNeg = TryGetNeighbor(pos + new int3(0, -1, 0));
			NativeArray<BlockState> nYPos = TryGetNeighbor(pos + new int3(0, 1, 0));
			NativeArray<BlockState> nZNeg = TryGetNeighbor(pos + new int3(0, 0, -1));
			NativeArray<BlockState> nZPos = TryGetNeighbor(pos + new int3(0, 0, 1));

			var verts = new NativeList<float3>(256, Allocator.Temp);
			var tris  = new NativeList<int3>(512, Allocator.Temp);

			BuildSolidMesh(center, nXNeg, nXPos, nYNeg, nYPos, nZNeg, nZPos, ref verts, ref tris);

			if (verts.Length > 0 && tris.Length > 0)
				OutColliders[index] = MeshCollider.Create(verts.AsArray(), tris.AsArray(), Filter, Material.Default);
			else
				OutColliders[index] = default;

			verts.Dispose();
			tris.Dispose();
		}

		// ── Helpers ─────────────────────────────────────────────────────
		private NativeArray<BlockState> TryGetNeighbor(int3 coord)
		{
			if (!ChunkMap.TryGetValue(coord, out Entity e)) return default;
			return !BlockDataLookup.TryGetValue(e, out ChunkComponent c) ? default : c.BlockData;
		}

		private bool IsSolid(BlockState b)
		{
			if (b.ID == 0) return false;
			if (b.ID >= BlockPrototypes.Length) return false;
			return !BlockPrototypes[b.ID].IsFluid;
		}

		// Returns solid? at world-relative position p (in chunk-local coords, can be -1 or ChunkSize).
		private bool SampleSolid(
			int3                    p,
			NativeArray<BlockState> c,
			NativeArray<BlockState> nXN, NativeArray<BlockState> nXP,
			NativeArray<BlockState> nYN, NativeArray<BlockState> nYP,
			NativeArray<BlockState> nZN, NativeArray<BlockState> nZP)
		{
			var cs = ChunkSize;

			if (p.x >= 0 && p.x < cs && p.y >= 0 && p.y < cs && p.z >= 0 && p.z < cs)
				return IsSolid(c[p.x | (p.y << 5) | (p.z << 10)]);

			NativeArray<BlockState> n  = default;
			int                     lx = p.x, ly = p.y, lz = p.z;

			if (p.x < 0)
			{
				n  = nXN;
				lx = cs - 1;
			}
			else if (p.x >= cs)
			{
				n  = nXP;
				lx = 0;
			}
			else if (p.y < 0)
			{
				n  = nYN;
				ly = cs - 1;
			}
			else if (p.y >= cs)
			{
				n  = nYP;
				ly = 0;
			}
			else if (p.z < 0)
			{
				n  = nZN;
				lz = cs - 1;
			}
			else if (p.z >= cs)
			{
				n  = nZP;
				lz = 0;
			}

			return n.IsCreated && IsSolid(n[lx | (ly << 5) | (lz << 10)]);
		}

		// Aggressive greedy mesh: 1 bit per voxel, no orientation/material distinction.
		private void BuildSolidMesh(
			NativeArray<BlockState> c,
			NativeArray<BlockState> nXN,   NativeArray<BlockState> nXP,
			NativeArray<BlockState> nYN,   NativeArray<BlockState> nYP,
			NativeArray<BlockState> nZN,   NativeArray<BlockState> nZP,
			ref NativeList<float3>  verts, ref NativeList<int3>    tris)
		{
			var cs    = ChunkSize;
			var maskF = new NativeArray<bool>(cs * cs, Allocator.Temp);
			var maskB = new NativeArray<bool>(cs * cs, Allocator.Temp);

			for (var dir = 0; dir < 3; dir++)
			{
				var axis1 = (dir + 1) % 3;
				var axis2 = (dir + 2) % 3;

				int3 dirMask = int3.zero;
				dirMask[dir] = 1;

				int3 itr = int3.zero;
				for (itr[dir] = -1; itr[dir] < cs; itr[dir]++)
				{
					var n = 0;
					for (itr[axis2] = 0; itr[axis2] < cs; itr[axis2]++)
					for (itr[axis1] = 0; itr[axis1] < cs; itr[axis1]++)
					{
						var curSolid = SampleSolid(itr, c, nXN, nXP, nYN, nYP, nZN, nZP);
						var cmpSolid = SampleSolid(itr + dirMask, c, nXN, nXP, nYN, nYP, nZN, nZP);

						maskF[n] = curSolid && !cmpSolid;
						maskB[n] = !curSolid && cmpSolid;
						n++;
					}

					var faceCoord = itr[dir] + 1;
					EmitMaskGreedy(maskF, dir, axis1, axis2, cs, faceCoord, +1, ref verts, ref tris);
					EmitMaskGreedy(maskB, dir, axis1, axis2, cs, faceCoord, -1, ref verts, ref tris);
				}
			}

			maskF.Dispose();
			maskB.Dispose();
		}

		private static void EmitMaskGreedy(
			NativeArray<bool>      mask,      int                  dir, int axis1, int axis2, int cs,
			int                    faceCoord, int                  normalSign,
			ref NativeList<float3> verts,     ref NativeList<int3> tris)
		{
			for (var j = 0; j < cs; j++)
			{
				var i = 0;
				while (i < cs)
				{
					if (!mask[i + j * cs])
					{
						i++;
						continue;
					}

					var w = 1;
					while (i + w < cs && mask[i + w + j * cs]) w++;

					var h    = 1;
					var done = false;
					while (j + h < cs && !done)
					{
						for (var k = 0; k < w; k++)
							if (!mask[i + k + (j + h) * cs])
							{
								done = true;
								break;
							}

						if (!done) h++;
					}

					int3 basePos = int3.zero;
					basePos[dir]   = faceCoord;
					basePos[axis1] = i;
					basePos[axis2] = j;

					int3 du = int3.zero;
					du[axis1] = w;
					int3 dv = int3.zero;
					dv[axis2] = h;

					float3 v0 = basePos;
					float3 v1 = basePos + du;
					float3 v2 = basePos + du + dv;
					float3 v3 = basePos + dv;

					var b = verts.Length;
					verts.Add(v0);
					verts.Add(v1);
					verts.Add(v2);
					verts.Add(v3);

					if (normalSign > 0)
					{
						tris.Add(new int3(b, b + 1, b + 2));
						tris.Add(new int3(b, b + 2, b + 3));
					}
					else
					{
						tris.Add(new int3(b, b + 2, b + 1));
						tris.Add(new int3(b, b + 3, b + 2));
					}

					for (var dy = 0; dy < h; dy++)
					for (var dx = 0; dx < w; dx++)
						mask[i + dx + (j + dy) * cs] = false;

					i += w;
				}
			}
		}
	}
}