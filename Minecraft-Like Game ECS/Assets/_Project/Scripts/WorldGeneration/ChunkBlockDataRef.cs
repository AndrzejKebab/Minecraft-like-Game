using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     Blittable handle to a chunk's flat BlockState buffer.
	///     WHY THIS EXISTS
	///     ───────────────
	///     <see cref="Unity.Collections.NativeArray{T}" /> carries a safety handle
	///     (AtomicSafetyHandle) that makes it non-blittable, so it cannot be stored
	///     as a value inside a NativeParallelHashMap.  This struct strips it down
	///     to a raw pointer + length — fully blittable, Burst-compatible, and safe
	///     as long as the owning NativeArray outlives this ref (guaranteed because
	///     we only build the map from <see cref="IsPopulated" /> chunk entities
	///     whose BlockData lives in a Persistent NativeArray on the ChunkComponent).
	/// </summary>
	public unsafe struct ChunkBlockDataRef
	{
		[NativeDisableUnsafePtrRestriction] private BlockState* _ptr;

		/// <summary>
		///     Creates a ref from the owning NativeArray.
		///     Call only on the main thread where safety handles are valid.
		/// </summary>
		public static ChunkBlockDataRef From(NativeArray<BlockState> array)
		{
			return new ChunkBlockDataRef
			       {
				       _ptr   = (BlockState*)array.GetUnsafePtr(),
				       Length = array.Length
			       };
		}

		/// <summary>Direct indexed read — no bounds check in Burst release builds.</summary>
		[BurstCompile]
		public BlockState Read(int index)
		{
			return _ptr[index];
		}

		/// <summary>
		///     Conditional write: only writes if the existing block matches
		///     <paramref name="requiredExistingID" />, or if
		///     <paramref name="requiredExistingID" /> is <see cref="REPLACE_ANY" />.
		///     Returns true when the write was performed.
		/// </summary>
		[BurstCompile]
		public bool TryWrite(int index, BlockState value, ushort requiredExistingID)
		{
			if (requiredExistingID != REPLACE_ANY && _ptr[index].ID != requiredExistingID)
				return false;

			_ptr[index] = value;
			return true;
		}

		public bool IsCreated => _ptr != null;
		public int  Length    { get; private set; }

		/// <summary>Pass as <c>requiredExistingID</c> to overwrite any block.</summary>
		public const ushort REPLACE_ANY = ushort.MaxValue;
	}
}