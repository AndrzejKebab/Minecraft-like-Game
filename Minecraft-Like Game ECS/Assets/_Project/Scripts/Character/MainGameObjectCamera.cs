using UnityEngine;

public class MainGameObjectCamera : MonoBehaviour
{
	public static Camera Instance;

	private void Awake()
	{
		Instance = GetComponent<Camera>();
	}
}