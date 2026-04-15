using _Project.WorldGeneration.Components;
using FastNoise2.Bindings;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration
{
	public class WorldSettings : MonoBehaviour
	{
		[Header("World Settings")]
		public int Seed = 1337;

		[Header("FastNoise2 Settings")] 
		public string TerrainNodeTree =
			"E@BHpEG@BD8wAQ@BkkCS4AAQ@BkNAAQ@BI@AgQAkH@BekQEA5qZGT8LAACAPxQDAADIQgQ=";
		public string PeaksAndValleysNodeTree =
			"IwCPwnU/CD0K174QH4XrvgkuAAE@BJDQAE@BCQc@AB6RBw=";

		public string ErosionNodeTree = "Iw@AIA+C@BD8QAACAPwkNAAQ@BJBg@AHpEFA==";
		public string CavesNodeTree =
			"FgIcCS4AAQ@BklCQs@BlRBDNzMw9G@AIMAgAw@ADgC@BCiQIzczMPgkJ@BPkIQH4XrPhjNzEw/DBIkCM3MzD4JCQ@ADBCCAE@BQzczMvhg@B/JAL/BAAL7FE4PgQKFwkNCQg@CQQQDuB4FPwt7FC4/BAOPwnU8DA==";

		public AnimationCurve BiomeHeightCurve;
		public AnimationCurve ErosionCurve;
		public AnimationCurve PeaksAndValleysCurve;

		private void Start()
		{
			InitializeWorldInECS();
		}

		private void InitializeWorldInECS()
		{
			EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

			Entity settingsEntity = em.CreateEntity();
			var worldSettings = new WorldSettingsSingleton
			                    {
				                    Seed                 = Seed,
				                    ContinentalnessCurve     = BiomeHeightCurve.ToNative(),
				                    ErosionCurve         = ErosionCurve.ToNative(),
				                    PeaksAndValleysCurve = PeaksAndValleysCurve.ToNative(),
				                    ContinentalnessNoise      = FastNoise.FromEncodedNodeTree(TerrainNodeTree),
				                    PeaksAndValleysNoise = FastNoise.FromEncodedNodeTree(PeaksAndValleysNodeTree),
				                    ErosionNoise = FastNoise.FromEncodedNodeTree(ErosionNodeTree),
				                    CavesNoise = FastNoise.FromEncodedNodeTree(CavesNodeTree)
			                    };
			
			
			em.AddComponentData(settingsEntity, worldSettings);

			Debug.Log($"[WorldSettings] World loaded with seed: {Seed} and injected into ECS.");
		}

		private void OnDestroy()
		{
			if (World.DefaultGameObjectInjectionWorld is null) return;
			EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

			Entity settingsEntity = em.CreateEntity();

			em.RemoveComponent(settingsEntity, typeof(WorldSettingsSingleton));

			Debug.Log("[WorldSettings] WorldSettingsSingleton removed from ECS.");
		}
	}
}