using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
	[SerializeField] private float moveSpeed = 5;
	[SerializeField] private float sprintSpeed = 7;
	[SerializeField] private float jumpForce = 5;
	[SerializeField] private LayerMask groundMask;

	private Vector3 velocity;
	private float rotateHorizontal;
	private float rotateVertical;
	private float fallMultiplier = 3.5f;
	public bool IsPlayer;
	public bool IsSprinting;
	private Rigidbody rigidbody;
	private Transform cam;
	[SerializeField] private bool isGrounded;

	private void Awake()
	{
		rigidbody = GetComponent<Rigidbody>();
		if (IsPlayer)
		{
			cam = GameObject.Find("Main Camera").transform;
		}
	}

	void Update()
	{
		transform.Rotate(Vector3.up * rotateHorizontal);

		if (IsPlayer)
		{
			cam.Rotate(Vector3.right * -rotateVertical);
		}

		isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.2f, groundMask);
	}

	public void SetVelocity(Vector3 velocityVector)
	{
		velocity = transform.forward * velocityVector.z + transform.right * velocityVector.x;
	}

	public void SetRotation(float HorizontalRotation)
	{
		rotateHorizontal = HorizontalRotation;
	}

	public void SetRotation(float HorizontalRotation, float VerticalRotation)
	{
		rotateHorizontal = HorizontalRotation;
		rotateVertical = VerticalRotation;
	}

	public void Jump()
	{
		if(isGrounded)
		{
			rigidbody.AddForce(transform.up * jumpForce, ForceMode.Impulse);
			isGrounded = false;
		}
	}

	private void FixedUpdate()
	{
		if(IsSprinting)
		{
			rigidbody.velocity = velocity * sprintSpeed;
		}
		else
		{
			rigidbody.velocity = velocity * moveSpeed;
		}

		if (rigidbody.velocity.y > 0)
		{
			rigidbody.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1);
		}
	}
}
