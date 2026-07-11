using UnityEngine;

public class MainGameObjectCamera : MonoBehaviour
{
	public static Camera Instance;

	private void Awake()
	{
		Instance = GetComponent<Camera>();
	}

	// statics survive when domain reload is disabled — reset on play mode entry
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		Instance = null;
	}
}