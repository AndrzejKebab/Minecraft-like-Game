using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration
{
	public class WorldSettings : MonoBehaviour
	{
		[Header("World Settings")] public int Seed = 1337;

		[Header("FastNoise2 Settings")] public string EncodedNodeTree =
			"E@BBZEG@BD8JFgokCMP1KD8JLgAB@BCQ0ABw@BgAACBACQc@BWRBA9Cle/GGZmZj8EA5qZGT8LAACAPxwDAABwQgQ=";

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
				                    BiomeHeightCurve     = BiomeHeightCurve.ToNative(),
				                    ErosionCurve         = ErosionCurve.ToNative(),
				                    PeaksAndValleysCurve = PeaksAndValleysCurve.ToNative(),
				                    EncodedNodeTree      = new FixedString512Bytes(EncodedNodeTree),
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