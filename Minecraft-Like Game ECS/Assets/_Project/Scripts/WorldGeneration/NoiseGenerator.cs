using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration
{
	/// <summary>
	///     PIPELINE (per chunk column):
	///     GenerateTerrainMap → NativeTexture2D{int} heightMap (absolute world Y per XZ column)
	///     ClassifyVoxel      → block prototype index (called per-voxel in TerrainShapePassJob)
	///     GenerateCaveMap lives here but is called ONLY from CavesPassJob, NOT terrain pass.
	///     Reason: terrain pass fills water first; cave pass carves after so underwater caves
	///     don't get re-flooded with water blocks.
	///     SEA LEVEL = 0.  No bedrock (world infinite all axes).
	///     ── NOISE FREQUENCY GUIDE (FastNoise2) ──────────────────────────────────
	///     Lower frequency = larger/smoother features.
	///     All GenUniformGrid2D calls use xStep=yStep=1 (1 sample per world unit).
	///     Control feature size via frequency inside encoded node tree in FastNoise2 NoiseTool.
	///     Continentalness  freq ~0.0006   → features ~1600 blocks  ocean/land shape
	///     Erosion          freq ~0.0015   → features  ~660 blocks  flat vs rugged zones
	///     PeaksAndValleys  freq ~0.004    → features  ~250 blocks  ridge detail
	///     River            freq ~0.001    → features ~1000 blocks  river networks
	///     ── FASTNOISE2 NODE TYPES ────────────────────────────────────────────────
	///     Continentalness : FractalFBm(Simplex2, oct=6, gain=0.5, lac=2.0)
	///     Erosion         : FractalFBm(Simplex2, oct=4, gain=0.5, lac=2.0)
	///     PeaksAndValleys : FractalRidged(Simplex2, oct=4, gain=0.5, lac=2.0)  output [0,1]
	///     River           : FractalFBm(Simplex2, oct=3, gain=0.5, lac=2.0)    abs() in code
	///     ── RECOMMENDED ANIMATIONCURVE KEYFRAMES ────────────────────────────────
	///     ContinentalnessHeight  ( x: cont [-1,1] → y: base height in blocks )
	///     (-1.0, -250)  deep ocean floor
	///     (-0.5,  -80)  open ocean
	///     (-0.2,  -15)  shallow sea
	///     (-0.05,  -2)  coast
	///     ( 0.0,    4)  sea-level land
	///     ( 0.3,   20)  low plains
	///     ( 0.6,   45)  inland highland
	///     ( 1.0,   80)  high plateau base
	///     ErosionCurve  ( x: eros [-1,1] → y: factor [0,1] )
	///     (-1.0, 1.00)   no erosion  → mountains allowed
	///     (-0.4, 0.70)
	///     ( 0.0, 0.30)   avg erosion → gentle hills only  ← most of world lives here
	///     ( 0.5, 0.05)
	///     ( 1.0, 0.00)   max erosion → flat plains
	///     PeaksAndValleysCurve  ( x: pv [0,1] → y: height bonus in blocks )
	///     ( 0.0, -15)   valley floor
	///     ( 0.2,   0)   neutral
	///     ( 0.5,  60)   hills
	///     ( 0.75, 200)  mountains
	///     ( 0.9,  380)  tall mountains
	///     ( 1.0,  520)  extreme peaks  (needs erosionFactor=1 to fully express)
	/// </summary>
	[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast,
		             FloatPrecision = FloatPrecision.Low)]
	public static class NoiseGenerator
	{
		// River tuning — adjust per-project, not per-chunk.
		// RiverHalfWidth: fraction of abs-noise range [0,1] treated as inside channel.
		//   0.08 = narrow stream   0.13 = normal river   0.20 = broad valley
		private const float RiverHalfWidth = 0.12f;

		// MaxRiverCarve: blocks carved at centreline (depth=0 at banks).
		private const float MaxRiverCarve = 28f;

		// ═════════════════════════════════════════════════════════════════════
		// GENERATE TERRAIN MAP
		// ═════════════════════════════════════════════════════════════════════

		/// <summary>
		///     Fills one 32×32 heightmap for a chunk column.
		///     Caller owns heightMap and must Dispose it after voxel loop.
		/// </summary>
		[BurstCompile]
		public static void GenerateTerrainMap(
			out NativeTexture2D<int> heightMap,
			ref FastNoise            continentalnessNoise,
			ref FastNoise            peaksAndValleysNoise,
			ref FastNoise            erosionNoise,
			ref FastNoise            riverNoise,
			ref int3                 chunkWorldPos,
			int                      chunkSize,
			int                      seed,
			ref NativeCurve          continentalnessHeight,
			ref NativeCurve          erosionCurve,
			ref NativeCurve          peaksAndValleysCurve)
		{
			heightMap = new NativeTexture2D<int>(new int2(chunkSize, chunkSize), Allocator.TempJob);

			var contMap = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			var pvMap   = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			var erosMap = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);
			var rivMap  = new NativeTexture2D<float>(new int2(chunkSize, chunkSize), Allocator.TempJob);

			continentalnessNoise.GenUniformGrid2D(contMap, out _,
			                                      chunkWorldPos.x, chunkWorldPos.z, chunkSize, chunkSize, 1, 1, seed);
			peaksAndValleysNoise.GenUniformGrid2D(pvMap, out _,
			                                      chunkWorldPos.x, chunkWorldPos.z, chunkSize, chunkSize, 1, 1, seed);
			erosionNoise.GenUniformGrid2D(erosMap, out _,
			                              chunkWorldPos.x, chunkWorldPos.z, chunkSize, chunkSize, 1, 1, seed);
			riverNoise.GenUniformGrid2D(rivMap, out _,
			                            chunkWorldPos.x, chunkWorldPos.z, chunkSize, chunkSize, 1, 1, seed);

			for (var x = 0; x < chunkSize; x++)
			for (var z = 0; z < chunkSize; z++)
				heightMap[x, z] = ComputeHeight(
				                                contMap[x, z],
				                                pvMap[x, z],
				                                erosMap[x, z],
				                                rivMap[x, z],
				                                ref continentalnessHeight,
				                                ref erosionCurve,
				                                ref peaksAndValleysCurve);

			contMap.Dispose();
			pvMap.Dispose();
			erosMap.Dispose();
			rivMap.Dispose();
		}

		// ═════════════════════════════════════════════════════════════════════
		// COMPUTE HEIGHT
		// ═════════════════════════════════════════════════════════════════════

		/// <summary>
		///     TerraForged-style height formula:
		///     baseHeight    = continentalnessHeight(cont)          ← ocean/land boundary + base elevation
		///     erosionFactor = erosionCurve(eros)                   ← [0,1] gates mountain formation
		///     peaksBonus    = peaksAndValleysCurve(pv)             ← ridge height in blocks
		///     riverCarve    = abs(river) channel carving           ← V-shaped valley
		///     finalHeight = baseHeight + erosionFactor * peaksBonus − riverCarve
		///     erosionFactor multiplication is the key smooth-terrain mechanism:
		///     Most world has avg erosion ≈ 0 → factor ≈ 0.30 → gentle hills.
		///     Only low-erosion zones (eros
		///     < -0.4) express full mountain height.
		///         This creates broad plains with mountain ranges, not world-wide spikes.
		///         River via abs( FBm) :
		///         FBm zero-crossings= long curvy lines. abs() makes those near-zero.
		///         Threshold + quadratic falloff → smooth V-valley profile.
		/// </summary>
		[BurstCompile]
		private static int ComputeHeight(
			float           cont,
			float           pv,
			float           eros,
			float           river,
			ref NativeCurve contSpline,
			ref NativeCurve erosSpline,
			ref NativeCurve pvSpline)
		{
			var baseHeight    = contSpline.Evaluate(cont);
			var erosionFactor = erosSpline.Evaluate(eros);
			var peaksBonus    = pvSpline.Evaluate(pv);

			// Combine: peaks only matter where erosion is low
			var h = baseHeight + erosionFactor * peaksBonus;

			// River carving: abs(FBm) near-zero lines = river centrelines
			var riverAbs = math.abs(river);
			if (!(riverAbs < RiverHalfWidth) || !(baseHeight > -20f)) return (int)math.round(h);
			var t     = 1f - riverAbs / RiverHalfWidth;        // 1 at centre, 0 at edge
			var carve = t * t * MaxRiverCarve;                 // quadratic V-profile
			carve *= math.saturate(1f - erosionFactor * 0.6f); // no deep rivers through mountains
			h     -= carve;

			return (int)math.round(h);
		}

		// ═════════════════════════════════════════════════════════════════════
		// GENERATE CAVE MAP
		// ═════════════════════════════════════════════════════════════════════

		/// <summary>
		///     Recommended FastNoise2 tree:
		///     FractalRidged(Simplex3D, oct=3, gain=0.5, lac=2.0, freq=0.02)
		///     Voxels with output above threshold in CavesPassJob are carved to air.
		/// </summary>
		[BurstCompile]
		public static void GenerateCaveMap(
			out NativeTexture3D<float> caveMap,
			ref FastNoise              caveNoise,
			ref int3                   chunkWorldPos,
			int                        chunkSize,
			int                        seed)
		{
			caveMap = new NativeTexture3D<float>(
			                                     new int3(chunkSize, chunkSize, chunkSize), Allocator.TempJob);
			caveNoise.GenUniformGrid3D(caveMap, out _,
			                           chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z,
			                           chunkSize, chunkSize, chunkSize, 1, 1, 1, seed);
		}

		// ═════════════════════════════════════════════════════════════════════
		// CLASSIFY VOXEL
		// ═════════════════════════════════════════════════════════════════════

		/// <summary>
		///     Block prototype index from world Y vs terrain height.
		///     Sea level = 0.  No bedrock.
		///     Index table (match GameDatabase / BlockData SO order):
		///     0 air | 1 stone | 2 dirt | 3 grass | 4 water | 5 sand
		///     Layering:
		///     depth 0 (surface)   → grass  (or sand near/below sea level)
		///     depth 1–4           → dirt   (or sand near/below sea level)
		///     depth 5+            → stone
		///     Above terrain ≤ Y0  → water
		///     Above terrain  > Y0 → air
		/// </summary>
		[BurstCompile]
		public static ushort ClassifyVoxel(int worldY, int terrainHeight)
		{
			if (worldY > terrainHeight)
				return worldY <= 0 ? (ushort)4 : (ushort)0; // water below sea level, air above

			var depth      = terrainHeight - worldY;
			var underwater = terrainHeight < 0;              // fully submerged column
			var beach      = terrainHeight is >= 0 and <= 5; // at/just above sea level

			return depth switch
			       {
				       0    => underwater || beach ? (ushort)5 : (ushort)3,
				       <= 4 => underwater || beach ? (ushort)5 : (ushort)2,
				       _    => 1
			       };
		}
	}
}