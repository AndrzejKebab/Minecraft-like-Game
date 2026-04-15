using _Project.WorldGeneration.Blocks;
using FastNoise2.Bindings;
using Unity.Collections;
using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	/// <summary>
	/// Global terrain + noise singleton. Populated once by WorldSettings on startup.
	///
	/// Curve field → job field mapping:
	///   ContinentalnessCurve  → TerrainShapePassJob.BiomeHeight
	///   ErosionCurve          → TerrainShapePassJob.ErosionCurve
	///   PeaksAndValleysCurve  → TerrainShapePassJob.PeaksAndValleysCurve
	///
	/// ChunkPopulateSystem must assign RiverNoise to TerrainShapePassJob.RiverNoise.
	/// </summary>
	public struct WorldSettingsSingleton : IComponentData
	{
		public int Seed;

		// ── FastNoise2 instances ──────────────────────────────────────────────
		public FastNoise ContinentalnessNoise;
		public FastNoise PeaksAndValleysNoise;
		public FastNoise ErosionNoise;
		public FastNoise RiverNoise;     // new — abs(FBm) river carving
		public FastNoise CavesNoise;

		// ── Terrain splines ───────────────────────────────────────────────────
		public NativeCurve ContinentalnessCurve; // cont[-1,1] → base height (blocks)
		public NativeCurve ErosionCurve;         // eros[-1,1] → factor [0,1]
		public NativeCurve PeaksAndValleysCurve; // pv  [0,1]  → bonus height (blocks)
	}

	/// <summary>
	/// All block prototypes indexed by prototype index (0 = air).
	/// </summary>
	public struct WorldBlockRegistrySingleton : IComponentData
	{
		public NativeArray<Block>               Blocks;
		public NativeArray<FixedString32Bytes>  BlockNames;
		public NativeArray<NativeVoxelMeshData> Meshes;

		public float TreeDensity;
		public int   MinTrunkHeight;
		public int   MaxTrunkHeight;

		public NativeArray<OreSettings> OreTypes;
	}
}