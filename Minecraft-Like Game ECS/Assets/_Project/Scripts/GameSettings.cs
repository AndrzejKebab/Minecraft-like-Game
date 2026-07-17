namespace _Project
{
	public static class GameSettings
	{
		// Granularity knobs. Each phase = ONE IJobParallelFor dispatch per frame, batched across N chunks.
		public const byte CHUNKS_PER_POPULATE_JOB  = 8;
		public const byte CHUNKS_PER_MESH_JOB      = 8;
		public const byte COLLIDER_BAKES_PER_FRAME = 8;

		// Entity churn budgets. Crossing a chunk boundary changes a whole shell of chunks;
		// creating/destroying them all in one frame (128 KB alloc + structural change each)
		// is the classic boundary hitch. Spread the work across frames instead.
		public const int CHUNK_CREATES_PER_FRAME  = 32;
		public const int CHUNK_DESTROYS_PER_FRAME = 32;

		// Player-radius (Chebyshev) inside which work is forced through urgent queue.
		public const byte URGENT_RADIUS = 2;

		public static readonly byte ViewDistanceInChunks = 16;
	}
}