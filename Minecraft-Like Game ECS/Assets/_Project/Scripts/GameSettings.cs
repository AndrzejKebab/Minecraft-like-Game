namespace _Project
{
	public static class GameSettings
	{
		// Per-frame chunk-work budget = logical cores minus 3 (leave headroom for the
		// main, render and audio threads). Every streaming phase uses this so the machine
		// scales the pipeline to its own core count.
		public static readonly int WorkerBudget =
			UnityEngine.Mathf.Max(1, UnityEngine.SystemInfo.processorCount - 3);

		// Granularity knobs. Each phase = ONE IJobParallelFor dispatch per frame, batched across N chunks.
		public static readonly int CHUNKS_PER_POPULATE_JOB  = WorkerBudget;
		public static readonly int CHUNKS_PER_MESH_JOB      = WorkerBudget;
		public static readonly int COLLIDER_BAKES_PER_FRAME = WorkerBudget;

		// Entity churn budgets. Crossing a chunk boundary changes a whole shell of chunks;
		// creating/destroying them all in one frame (128 KB alloc + structural change each)
		// is the classic boundary hitch. Spread the work across frames instead.
		public static readonly int CHUNK_CREATES_PER_FRAME  = WorkerBudget;
		public static readonly int CHUNK_DESTROYS_PER_FRAME = WorkerBudget;

		// Player-radius (Chebyshev) inside which work is forced through urgent queue.
		public const byte URGENT_RADIUS = 2;

		public static readonly byte ViewDistanceInChunks = 16;
	}
}