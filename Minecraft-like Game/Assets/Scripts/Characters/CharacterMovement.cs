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
	private float fallMultiplier = 1.5f;
	public bool IsPlayer;
	public bool IsSprinting;
	private Rigidbody rb;
	private Transform cam;
	[SerializeField] private bool isGrounded;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
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

		isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.3f, groundMask);
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
			rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
			//velocity += transform.up * jumpForce;
			isGrounded = false;
		}
	}

	private void FixedUpdate()
	{
		if(IsSprinting)
		{
			rb.velocity = velocity * sprintSpeed;
		}
		else
		{
			rb.velocity = velocity * moveSpeed;
		}
		
		if (rb.velocity.y < 0)
		{
			rb.velocity += transform.up * Physics.gravity.y * fallMultiplier;
		}
	}
}