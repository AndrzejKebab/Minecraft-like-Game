using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project
{
	public class LoadMainScene : MonoBehaviour
	{
		private void Awake()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
		}

		// static event subscription survives when domain reload is disabled —
		// clear on play mode entry (idempotent)
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name != "Main") return;
			SceneManager.SetActiveScene(scene);
			Debug.Log("[SetSceneActive] Main scene loaded additively and set to active.");
				
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}
	}
}
