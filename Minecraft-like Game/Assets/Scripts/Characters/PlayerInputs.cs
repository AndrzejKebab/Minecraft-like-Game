using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(CharacterMovement))]
public class PlayerInputs : MonoBehaviour
{
	private Vector3 movement;
	private float mouseHorizontal;
	private float mouseVertical;

	private CharacterMovement characterMovement;

	private void Awake()
	{
		characterMovement = GetComponent<CharacterMovement>();
		characterMovement.IsPlayer = true;
		Cursor.lockState = CursorLockMode.Locked;
	}

	private void Update()
	{
		movement.x = Input.GetAxisRaw("Horizontal");
		movement.z = Input.GetAxisRaw("Vertical");
		mouseHorizontal = Input.GetAxis("Mouse X");
		mouseVertical = Input.GetAxis("Mouse Y");
		movement.Normalize();

		if (Input.GetButtonDown("Sprint"))
		{
			characterMovement.IsSprinting = true;
		}
		else if(Input.GetButtonUp("Sprint"))
		{
			characterMovement.IsSprinting = false;
		}

		if (Input.GetButtonDown("Jump"))
		{
			characterMovement.Jump();
		}

		characterMovement.SetVelocity(movement);
		characterMovement.SetRotation(mouseHorizontal, mouseVertical);
	}
}
