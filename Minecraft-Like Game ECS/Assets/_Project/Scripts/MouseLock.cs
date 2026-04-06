using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project
{
	public class MouseLock : MonoBehaviour
	{
		private void Awake()
		{
			Cursor.lockState = CursorLockMode.Locked;
		}

		private void Update()
		{
			if (Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
			}
		}
	}
}