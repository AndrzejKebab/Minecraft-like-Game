using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
	public bool IsGrounded;
	public bool IsSprinting;

	[SerializeField] private float movementSpeed = 0.75f;
	[SerializeField] private float sprintSpeed = 1;
	[SerializeField] private float jumpForce = 2.25f;
	[SerializeField] private float playerWidth = 0.3f;
	[SerializeField] private float gravity = -9.807f;

	private Transform camera;
	private World world;

	private float horizontal;
	private float vertical;
	private float mouseHorizontal;
	private float mouseVertical;
	private Vector3 velocity;
	private float verticalMomentum = 0;
	private bool jumpRequest;

	private void Start()
	{
		camera = Camera.main.transform;
		world = GameObject.Find("World").GetComponent<World>();
	}

	private void Update()
	{
		GetPlayerInputs();
	}

	private void FixedUpdate()
	{
		CalculateVelocity();

		if (jumpRequest)
		{
			Jump();
		}

		transform.Rotate(Vector3.up * mouseHorizontal * 5);
		camera.Rotate(Vector3.right * -mouseVertical * 5);
		transform.Translate(velocity, Space.World);
	}

	private void CalculateVelocity()
	{
		if(verticalMomentum > gravity)
		{
			verticalMomentum += Time.fixedDeltaTime * gravity;
		}

		if (IsSprinting)
		{
			velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * sprintSpeed;
		}
		else
		{
			velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * movementSpeed;
		}

		velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

		if((velocity.z > 0 && Front) || (velocity.z < 0 && Back))
		{
			velocity.z = 0;
		}

		if ((velocity.x > 0 && Right) || (velocity.x < 0 && Left))
		{
			velocity.x = 0;
		}

		if(velocity.y < 0)
		{
			velocity.y = checkDownSpeed(velocity.y);
		}
		else if(velocity.z > 0)
		{
			velocity.y = checkUpSpeed(velocity.y);
		}
	}

	private void GetPlayerInputs()
	{
		horizontal = Input.GetAxis("Horizontal");
		vertical = Input.GetAxis("Vertical");
		mouseHorizontal = Input.GetAxis("Mouse X");
		mouseVertical = Input.GetAxis("Mouse Y");

		if (Input.GetButtonDown("Sprint"))
		{
			IsSprinting = true;
		}
		else if(Input.GetButtonUp("Sprint"))
		{
			IsSprinting = false;
		}

		if(IsGrounded && Input.GetButtonDown("Jump"))
		{
			jumpRequest = true;
		}
	}

	private void Jump()
	{
		verticalMomentum = jumpForce;
		IsGrounded = false;
		jumpRequest = false;
	}

	private float checkDownSpeed (float downSpeed)
	{
		if (
			world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth))
		  )
		{
			IsGrounded = true;
			return 0;
		}
		else
		{
			IsGrounded = false;
			return downSpeed;
		}
	}

	private float checkUpSpeed(float upSpeed)
	{
		if (
			world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2 + upSpeed, transform.position.z - playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2 + upSpeed, transform.position.z - playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2 + upSpeed, transform.position.z + playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2 + upSpeed, transform.position.z + playerWidth))
		  )
		{
			return 0;
		}
		else
		{
			return upSpeed;
		}
	}

	public bool Front
	{
		get { if (
					world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z + playerWidth)) ||
					world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1, transform.position.z + playerWidth))
				)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}

	public bool Back
	{
		get
		{
			if (
					world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z - playerWidth)) ||
					world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1, transform.position.z - playerWidth))
				)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}

	public bool Left
	{
		get
		{
			if (
					world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y, transform.position.z)) ||
					world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 1, transform.position.z))
				)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}

	public bool Right
	{
		get
		{
			if (
					world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y, transform.position.z)) ||
					world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 1, transform.position.z))
				)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}
