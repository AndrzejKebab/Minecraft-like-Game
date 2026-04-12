using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project
{
	public class LoadMainScene : MonoBehaviour
	{
		private void Awake()
		{
			SceneManager.LoadScene("Main", LoadSceneMode.Additive);
			Debug.Log("[LoadMainScene] Main scene loaded additively.");
		}
	}
}
