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

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name != "Main") return;
			SceneManager.SetActiveScene(scene);
			Debug.Log("[SetSceneActive] Main scene loaded additively and set to active.");
				
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}
	}
}
