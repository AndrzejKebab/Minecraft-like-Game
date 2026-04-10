#if !UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project
{
	public class LoadMainScene : MonoBehaviour
	{
		private void Start()
		{
			SceneManager.LoadScene("Main", LoadSceneMode.Additive);
		}
	}
}
#endif