using System;
using _Project.WorldGeneration.Components;
using FastNoise2.Bindings;
using FastNoise2.Generators;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration
{
	public class WorldSettings : MonoBehaviour
	{
		[Header("World Settings")] public int Seed = 1337;

		[Header("FastNoise2 Node Trees")] public string ContinentalnessEncoded =
			"E@BPpDG@BD8wAQ@BkkCS4AAQ@BkNAAQ@BI@AgQAkH@BekQEA5qZGT8LAACAPxQDAADIQgQ=";

		public string PeaksAndValleysEncoded = "KQAB@BCQ8AB@CkH@BekQU";
		public string ErosionEncoded         = "Iw@AIA+C@BD8QAACAPwkNAAQ@BJBg@AHpEFA==";
		public string RiverEncoded           = "KQAB@BCRkJE@BPpDMAE@BJDQAE@BCQY@AB6RCQ=";

		public string CavesEncoded =
			"FgIcCS4AAQ@BklCQs@BlRBDNzMw9G@AIMAgAw@ADgC@BCiQIzczMPgkJ@BPkIQH4XrPhjNzEw/DBIkCM3MzD4JCQ@ADBCCAE@BQzczMvhg@B/JAL/BAAL7FE4PgQKFwkNCQg@CQQQDuB4FPwt7FC4/BAOPwnU8DA=="; // FractalRidged Simplex3D freq~0.02

		[Header("Terrain Splines (legacy generator)")] public AnimationCurve ContinentalnessCurve;

		public AnimationCurve ErosionCurve;
		public AnimationCurve PeaksAndValleysCurve;

		[Header("TerraGen (ReTerraForged-style pipeline)")]
		[Tooltip("Blocks from the sea (always world Y 0) down to the deepest ocean floor")]
		public int OceanDepth = 256;

		[Tooltip("Typical big-mountain elevation in blocks; rare peaks reach ~2x this")]
		public int MountainHeight = 512;

		[Tooltip("Size of continent voronoi cells in blocks")]
		public int ContinentScale = 3000;

		[Tooltip("Size of terrain-type regions (plains/hills/mountains provinces)")]
		public int TerrainRegionSize = 1200;

		[Tooltip("Size of climate/biome regions")]
		public int BiomeSize = 800;

		public bool RiversEnabled = true;

		[Tooltip("RTF-style branching river networks (flow to the sea) vs simple voronoi rivers")]
		public bool UseRiverNetworks = true;

		[Tooltip("Main rivers per continent (RTF riverCount)")]
		public int RiverCount = 9;

		[Tooltip("Functional-erosion mountains (RTF fancy mountains) — pricier but prettier")]
		public bool FancyMountains = true;

		[Tooltip("Droplet erosion filter, applied once per cached tile (RTF filters)")]
		public bool ErosionEnabled = true;

		[Tooltip("Erosion droplets per 16-block cell (RTF default 135)")]
		public int ErosionDropletsPerChunk = 135;

		private void Awake()
		{
			InitializeWorldInECS();
			InitializeNoises();
		}
		
		private void OnDestroy()
		{
			if (World.DefaultGameObjectInjectionWorld is null) return;
			EntityManager em    = World.DefaultGameObjectInjectionWorld.EntityManager;
			EntityQuery   query = em.CreateEntityQuery(typeof(WorldSettingsSingleton));
			Entity        ent   = query.GetSingletonEntity();
			var           comp  = em.GetComponentData<WorldSettingsSingleton>(ent);

			comp.ContinentalnessNoise.Dispose();
			comp.PeaksAndValleysNoise.Dispose();
			comp.ErosionNoise.Dispose();
			comp.RiverNoise.Dispose();
			comp.CavesNoise.Dispose();
			comp.ContinentalnessCurve.Dispose();
			comp.PeaksAndValleysCurve.Dispose();
			comp.ErosionCurve.Dispose();
			
			if (!query.IsEmpty) em.DestroyEntity(ent);
		}

		private void InitializeNoises()
		{
			CreateContinentalnessNoise();
			CreateErosionNoise();
			CreatePeaksAndValleysNoise();
			CreateRiverNoise();
			CreateCavesNoise();
		}

		private void CreateContinentalnessNoise()
		{
			NoiseNode baseNoise = Noise.Simplex(1000).Fbm(0.6f, 1, 4, 2.5f)
			                           .DomainRotatePlane(PlaneRotationType.ImproveXZPlanes)
			                           .DomainWarpSimplex(100, 500, VectorizationScheme.GradientOuterProduct).PowInt();
			NoiseNode remap      = baseNoise.Remap(0, 1, -1, 1);
			NoiseNode finalNoise = baseNoise.MinSmooth(remap, 1);
			ContinentalnessEncoded = finalNoise.Encode();
		}

		private void CreatePeaksAndValleysNoise()
		{
			NoiseNode baseNoise = Noise.Simplex(5000).Fbm(0.5f, 1, 4).Abs().Remap().PingPong().Remap();
			PeaksAndValleysEncoded = baseNoise.Encode();
		}

		private void CreateErosionNoise()
		{
			NoiseNode baseNoise =
				Noise.Simplex(1000, -1).Fbm(0.5f, 1, 4).DomainRotatePlane(PlaneRotationType.ImproveXZPlanes)
				     .SignedSqrt() * Noise.Perlin(1000, 1);
			ErosionEncoded = baseNoise.Encode();
		}

		private void CreateRiverNoise()
		{
			DomainWarpNode baseNoise = Noise.Simplex(2000).Fbm(0.5f, 1, 4)
			                                .DomainWarpSimplex(50, 1000, VectorizationScheme.GradientOuterProduct);
			NoiseNode finalNoise = baseNoise.MinSmooth(baseNoise * 2, 1).Abs();
			RiverEncoded = finalNoise.Encode();
		}

		private void CreateCavesNoise()
		{
			NoiseNode baseNoise1 = Noise.Value(47.5f, 0, 0.46f, 0.8f).DomainAxisScale(1, 0.4f);
			NoiseNode baseNoise2 = Noise.Value(44, 1, -0.4f, 0.5f).DomainAxisScale(1, 0.4f);
			NoiseNode baseNoise3 = Noise.Value(8).Fbm(0.52f, 0.68f) * 0.015f;
			CellularDistanceNode celDist = Noise.CellularDistance(660, 0.1f, -2.5f)
			                                    .WithDistanceFunction(DistanceFunction.Hybrid)
			                                    .WithReturnType(CellularReturnType.Index0Sub1)
			                                    .WithGridJitter(baseNoise1)
			                                    .WithSizeJitter(baseNoise2);
			NoiseNode offSet        = celDist.SeedOffset(1).DomainRotatePlane(PlaneRotationType.ImproveXZPlanes);
			NoiseNode finalNoise = offSet.MinSmooth(celDist, 0.18f) - baseNoise3;
			CavesEncoded = finalNoise.Encode();
		}

		private void InitializeWorldInECS()
		{
			EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

			var worldSettings = new WorldSettingsSingleton
			                    {
				                    Seed = Seed,

				                    ContinentalnessNoise = CreateNoise(ContinentalnessEncoded),
				                    PeaksAndValleysNoise = CreateNoise(PeaksAndValleysEncoded),
				                    ErosionNoise         = CreateNoise(ErosionEncoded),
				                    RiverNoise           = CreateNoise(RiverEncoded),
				                    CavesNoise           = CreateNoise(CavesEncoded),
				                    ContinentalnessCurve = ContinentalnessCurve.ToNative(),
				                    ErosionCurve         = ErosionCurve.ToNative(),
				                    PeaksAndValleysCurve = PeaksAndValleysCurve.ToNative()
			                    };

			Entity settingsEntity = em.CreateEntity();
			em.AddComponentData(settingsEntity, worldSettings);

			TerraGen.TerraGenSettings terraSettings = TerraGen.TerraGenSettings.Default(Seed);
			terraSettings.OceanDepth        = OceanDepth;
			terraSettings.MountainHeight    = MountainHeight;
			terraSettings.ContinentScale    = ContinentScale;
			terraSettings.TerrainRegionSize = TerrainRegionSize;
			terraSettings.BiomeSize         = BiomeSize;
			terraSettings.RiversEnabled           = RiversEnabled;
			terraSettings.UseRiverNetworks        = UseRiverNetworks;
			terraSettings.RiverCount              = RiverCount;
			terraSettings.FancyMountains          = FancyMountains;
			terraSettings.ErosionEnabled          = ErosionEnabled;
			terraSettings.ErosionDropletsPerChunk = ErosionDropletsPerChunk;
			em.AddComponentData(settingsEntity, terraSettings);

			Debug.Log($"[WorldSettings] Loaded seed={Seed} (TerraGen pipeline)");
		}

		/// <summary>
		///     Creates FastNoise from encoded string. Falls back to plain Simplex if empty
		///     so chunks generate something visible while node trees aren't set yet.
		/// </summary>
		private static FastNoise CreateNoise(string encoded)
		{
			if (!string.IsNullOrWhiteSpace(encoded))
				return FastNoise.FromEncodedNodeTree(encoded);

			Debug.LogWarning("[WorldSettings] Empty encoded noise string — falling back to Simplex. Set node trees in Inspector.");
			return new FastNoise("Simplex");
		}
	}
}