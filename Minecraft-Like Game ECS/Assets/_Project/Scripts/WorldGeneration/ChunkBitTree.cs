using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     Fixed 64×64×64 bit volume over chunk coordinates, centered on a player chunk,
	///     with an OR-reduction summary for O(popcount) empty-region skipping — a flat,
	///     pointer-free analogue of Sodium's section tree.
	///
	///     Layout: X is packed into the 64 bits of a ulong word; there is one word per
	///     (z, y) row → <c>_leaf[z*64 + y]</c>, bit <c>x</c>. A reduction level sits on top:
	///     <c>_rowAny[z]</c> has bit <c>y</c> set iff that (z,y) leaf word is non-zero, so
	///     queries skip empty rows without touching leaf words.
	///
	///     Both backing arrays are pointer-backed (UnsafeList), so the struct can be passed
	///     BY VALUE into a Burst job and <see cref="Set" /> still mutates the shared memory —
	///     there is deliberately no plain-field summary (it wouldn't propagate out of a job
	///     copy). <see cref="AnySet" /> is derived from <c>_rowAny</c> on demand.
	///
	///     Usage contract: only ever <see cref="Set" /> bits and <see cref="ResetTo" /> the
	///     whole volume (the occlusion pass rebuilds it fresh). Individual bit-clears are
	///     intentionally unsupported so the reduction stays trivially correct.
	/// </summary>
	public unsafe struct ChunkBitTree : IDisposable
	{
		public const  int DIM        = 64;
		public const  int CENTER     = DIM / 2;   // player chunk maps here
		private const int LEAF_WORDS = DIM * DIM; // 4096 ulongs (one per z,y row)

		private UnsafeList<ulong> _leaf;   // [z*64 + y], bit x
		private UnsafeList<ulong> _rowAny; // [z], bit y

		/// <summary> World chunk coord that sits at the volume center (CENTER,CENTER,CENTER). </summary>
		public int3 Origin;

		public bool IsCreated => _leaf.IsCreated;

		public ChunkBitTree(Allocator allocator)
		{
			_leaf = new UnsafeList<ulong>(LEAF_WORDS, allocator);
			_leaf.Resize(LEAF_WORDS, NativeArrayOptions.ClearMemory);
			_rowAny = new UnsafeList<ulong>(DIM, allocator);
			_rowAny.Resize(DIM, NativeArrayOptions.ClearMemory);
			Origin = int3.zero;
		}

		public void Dispose()
		{
			if (_leaf.IsCreated) _leaf.Dispose();
			if (_rowAny.IsCreated) _rowAny.Dispose();
		}

		/// <summary> Re-center on a new player chunk and zero all bits. </summary>
		public void ResetTo(int3 playerChunk)
		{
			Origin = playerChunk;
			UnsafeUtility.MemClear(_leaf.Ptr, LEAF_WORDS * sizeof(ulong));
			UnsafeUtility.MemClear(_rowAny.Ptr, DIM * sizeof(ulong));
		}

		/// <summary> World chunk coord → local (0..63) volume coord. False if out of range. </summary>
		public bool TryToLocal(int3 worldChunk, out int3 local)
		{
			local = worldChunk - Origin + CENTER;
			return (uint)local.x < DIM && (uint)local.y < DIM && (uint)local.z < DIM;
		}

		public int3 ToWorld(int3 local)
		{
			return local + Origin - CENTER;
		}

		/// <summary> Set the bit at a local coord (assumes 0..63; caller uses TryToLocal). </summary>
		public void Set(int3 local)
		{
			_leaf.Ptr[local.z * DIM + local.y] |= 1ul << local.x;
			_rowAny.Ptr[local.z]               |= 1ul << local.y;
		}

		public bool Test(int3 local)
		{
			if ((uint)local.x >= DIM || (uint)local.y >= DIM || (uint)local.z >= DIM) return false;
			return (_leaf.Ptr[local.z * DIM + local.y] & (1ul << local.x)) != 0;
		}

		public bool TestWorld(int3 worldChunk)
		{
			return TryToLocal(worldChunk, out int3 l) && Test(l);
		}

		/// <summary> True if any bit is set anywhere. </summary>
		public bool AnySet
		{
			get
			{
				for (var z = 0; z < DIM; z++)
					if (_rowAny.Ptr[z] != 0)
						return true;
				return false;
			}
		}

		/// <summary>
		///     True if any bit is set inside the given inclusive local AABB. Uses the row
		///     reduction to skip empty y-rows before touching leaf words.
		/// </summary>
		public bool AnyInBox(int3 minLocal, int3 maxLocal)
		{
			var minX = math.max(0, minLocal.x);
			var maxX = math.min(DIM - 1, maxLocal.x);
			var minY = math.max(0, minLocal.y);
			var maxY = math.min(DIM - 1, maxLocal.y);
			var minZ = math.max(0, minLocal.z);
			var maxZ = math.min(DIM - 1, maxLocal.z);
			if (minX > maxX || minY > maxY || minZ > maxZ) return false;

			ulong xMask = MaskRange(minX, maxX);
			ulong yMask = MaskRange(minY, maxY);

			for (var z = minZ; z <= maxZ; z++)
			{
				var yRow = _rowAny.Ptr[z] & yMask;
				while (yRow != 0)
				{
					var y = math.tzcnt(yRow);
					yRow &= yRow - 1;
					if ((_leaf.Ptr[z * DIM + y] & xMask) != 0) return true;
				}
			}

			return false;
		}

		/// <summary> Inclusive bit range mask [lo,hi] within a 64-bit word. </summary>
		private static ulong MaskRange(int lo, int hi)
		{
			var count = hi - lo + 1;
			var span  = count >= 64 ? ~0ul : (1ul << count) - 1ul;
			return span << lo;
		}
	}
}
