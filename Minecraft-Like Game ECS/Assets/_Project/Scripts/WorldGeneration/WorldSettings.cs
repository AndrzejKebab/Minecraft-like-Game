using _Project.WorldGeneration.Components;
using FastNoise2.Bindings;
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

		[Header("Terrain Splines")] public AnimationCurve ContinentalnessCurve;

		public AnimationCurve ErosionCurve;
		public AnimationCurve PeaksAndValleysCurve;

		private void Start()
		{
			InitializeWorldInECS();
		}

		private void OnDestroy()
		{
			if (World.DefaultGameObjectInjectionWorld is null) return;
			EntityManager em    = World.DefaultGameObjectInjectionWorld.EntityManager;
			EntityQuery   query = em.CreateEntityQuery(typeof(WorldSettingsSingleton));
			if (!query.IsEmpty) em.DestroyEntity(query.GetSingletonEntity());
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

			em.AddComponentData(em.CreateEntity(), worldSettings);
			Debug.Log($"[WorldSettings] Loaded seed={Seed}");
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