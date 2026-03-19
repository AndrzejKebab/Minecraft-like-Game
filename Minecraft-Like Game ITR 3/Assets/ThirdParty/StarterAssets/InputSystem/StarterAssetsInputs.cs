using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorInputForLook = true;

		public void Awake()
		{
			Cursor.lockState = CursorLockMode.Locked;
		}

		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
		
		private void OnPause(InputValue value)
		{
			SetCursorState(value.isPressed);
		}


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		
		private void SetCursorState(bool newState)
		{
			if(Cursor.lockState == CursorLockMode.Locked)
			{
				Cursor.lockState = CursorLockMode.None;
			}
			else
			{
				Cursor.lockState = CursorLockMode.Locked;
			}
		}
	}
}