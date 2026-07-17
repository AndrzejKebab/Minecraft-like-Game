namespace _Project
{
	public static class GameSettings
	{
		// Per-frame chunk-work budget = logical cores minus 3 (leave headroom for the
		// main, render and audio threads). Every streaming phase uses this so the machine
		// scales the pipeline to its own core count.
		private static readonly int workerBudget =
			UnityEngine.Mathf.Max(1, UnityEngine.SystemInfo.processorCount - 3);

		// Granularity knobs. Each phase = ONE IJobParallelFor dispatch per frame, batched across N chunks.
		public static readonly int ChunksPerPopulateJob  = workerBudget;
		public static readonly int ChunksPerMeshJob      = workerBudget;
		public static readonly int ColliderBakesPerFrame = workerBudget;

		// Entity churn budgets. Crossing a chunk boundary changes a whole shell of chunks;
		// creating/destroying them all in one frame (128 KB alloc + structural change each)
		// is the classic boundary hitch. Spread the work across frames instead.
		public static readonly int ChunkCreatesPerFrame  = workerBudget;
		public static readonly int ChunkDestroysPerFrame = workerBudget;

		// Player-radius (Chebyshev) inside which work is forced through urgent queue.
		public const byte URGENT_RADIUS = 2;

		public static readonly byte ViewDistanceInChunks = 16;
	}
}