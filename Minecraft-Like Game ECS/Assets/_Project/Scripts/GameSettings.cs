namespace _Project
{
	public static class GameSettings
	{
		// Granularity knobs. Each phase = ONE IJobParallelFor dispatch per frame, batched across N chunks.
		public const byte CHUNKS_PER_POPULATE_JOB  = 8;
		public const byte CHUNKS_PER_MESH_JOB      = 8;
		public const byte COLLIDER_BAKES_PER_FRAME = 8;

		// Player-radius (Chebyshev) inside which work is forced through urgent queue.
		public const byte URGENT_RADIUS = 2;

		public static readonly byte ViewDistanceInChunks = 8;
	}
}