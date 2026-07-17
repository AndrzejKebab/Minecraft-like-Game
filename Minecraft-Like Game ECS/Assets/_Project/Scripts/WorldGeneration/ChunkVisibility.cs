using _Project.WorldGeneration.Blocks;
using Unity.Collections;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     Sodium-style per-chunk visibility mask (bit <c>from*8+to</c> = "a sightline
	///     entering through face <c>from</c> can leave through face <c>to</c>"), computed by
	///     flood-filling the chunk's non-opaque cells and connecting every pair of chunk
	///     faces each air region touches.
	///
	///     Needs only the chunk's OWN BlockData — no neighbour halo — so the mask is
	///     produced at populate time (long before the chunk is meshable, which needs all 6
	///     neighbours) and refreshed by the mesh job whenever a chunk is re-meshed after a
	///     block edit. Shared by ChunkPopulateJob and GreedyMeshJob.
	/// </summary>
	public static class ChunkVisibility
	{
		/// <summary> Every from→to face pair set — a fully see-through (e.g. all-air) chunk. </summary>
		public const ulong AllFacesConnected =
			(0x3Ful << 0)  | (0x3Ful << 8)  | (0x3Ful << 16) |
			(0x3Ful << 24) | (0x3Ful << 32) | (0x3Ful << 40);

		/// <summary>
		///     Flood-fill the chunk's non-opaque cells; each connected empty region that
		///     touches a set of chunk faces means a sightline can pass between any pair of
		///     those faces. A solid-opaque chunk returns 0 (blocks every path).
		/// </summary>
		public static ulong ComputeMask(in NativeArray<BlockState> blocks, in NativeArray<Block> prototypes)
		{
			const int cs = ChunkData.CHUNK_SIZE; // 32
			const int n  = cs * cs * cs;

			var visited = new NativeArray<bool>(n, Allocator.Temp, NativeArrayOptions.ClearMemory);
			var stack   = new NativeList<int>(256, Allocator.Temp);

			ulong mask = 0;

			for (var start = 0; start < n; start++)
			{
				if (visited[start]) continue;
				if (IsOccluder(blocks[start], prototypes))
				{
					visited[start] = true;
					continue;
				}

				// flood one connected non-opaque region, collecting the faces it touches
				var faces = 0;
				stack.Clear();
				stack.Add(start);
				visited[start] = true;

				while (stack.Length > 0)
				{
					var idx = stack[stack.Length - 1];
					stack.RemoveAt(stack.Length - 1);

					var x = idx & 31;
					var y = (idx >> 5) & 31;
					var z = (idx >> 10) & 31;

					if (x == 0) faces      |= 1 << 0;
					if (x == cs - 1) faces |= 1 << 1;
					if (y == 0) faces      |= 1 << 2;
					if (y == cs - 1) faces |= 1 << 3;
					if (z == 0) faces      |= 1 << 4;
					if (z == cs - 1) faces |= 1 << 5;

					if (x > 0) TryPush(ref stack, visited, blocks, prototypes, idx - 1);
					if (x < cs - 1) TryPush(ref stack, visited, blocks, prototypes, idx + 1);
					if (y > 0) TryPush(ref stack, visited, blocks, prototypes, idx - 32);
					if (y < cs - 1) TryPush(ref stack, visited, blocks, prototypes, idx + 32);
					if (z > 0) TryPush(ref stack, visited, blocks, prototypes, idx - 1024);
					if (z < cs - 1) TryPush(ref stack, visited, blocks, prototypes, idx + 1024);
				}

				mask |= PairsFromFaces(faces);
				if (mask == AllFacesConnected) break; // can't gain more connectivity
			}

			visited.Dispose();
			stack.Dispose();
			return mask;
		}

		private static void TryPush(ref NativeList<int> stack, NativeArray<bool> visited,
		                            in NativeArray<BlockState> blocks, in NativeArray<Block> prototypes, int idx)
		{
			if (visited[idx]) return;
			visited[idx] = true;
			if (IsOccluder(blocks[idx], prototypes)) return; // marked visited so we never revisit
			stack.Add(idx);
		}

		// a sightline is blocked only by a FULL opaque cube; fluids and custom meshes
		// (slabs, fences, foliage) let light through → treat as non-occluders
		private static bool IsOccluder(BlockState state, in NativeArray<Block> prototypes)
		{
			if (state.IsEmpty || state.ID == 0) return false;
			Block block = prototypes[state.ID];
			if (block.IsFluid) return false;
			return block.MeshID == 0 && !block.IsTransparent;
		}

		/// <summary> Set bit (a*8+b) for every ordered pair of faces present in <paramref name="faces" />. </summary>
		private static ulong PairsFromFaces(int faces)
		{
			ulong m = 0;
			for (var a = 0; a < 6; a++)
			{
				if ((faces & (1 << a)) == 0) continue;
				for (var b = 0; b < 6; b++)
					if ((faces & (1 << b)) != 0)
						m |= 1ul << (a * 8 + b);
			}

			return m;
		}
	}
}
