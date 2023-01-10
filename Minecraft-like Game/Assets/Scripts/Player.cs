using TMPro;
using UnityEngine;

public class Player : MonoBehaviour
{
	private bool IsGrounded;
	private bool IsSprinting;

	private Transform cam;
	private World world;

	[Header("Player Data")]
	[SerializeField] private float walkSpeed = 3f;
	[SerializeField] private float sprintSpeed = 6f;
	[SerializeField] private float jumpForce = 5f;
	[SerializeField] private float gravity = -9.8f;
	[SerializeField] private float playerWidth = 0.15f;

	private float horizontal;
	private float vertical;
	private float mouseHorizontal;
	private float mouseVertical;
	private Vector3 velocity;
	private float verticalMomentum = 0;
	private bool jumpRequest;

	[SerializeField] private Transform highlightBlock;
	[SerializeField] private Transform placeBlock;
	[SerializeField] private float checkIncrement = 0.01f;
	[SerializeField] private float reach = 8f;

	public byte SelectedBlockIndex = 1;

	private void Start() 
	{
		cam = GameObject.Find("Main Camera").transform;
		world = GameObject.Find("World").GetComponent<World>();
		Cursor.lockState = CursorLockMode.Locked;
	}

	private void FixedUpdate()
	{
		CalculateVelocity();

		if (jumpRequest) Jump();

		transform.Translate(velocity, Space.World);
	}

	private void Update()
	{
		GetPlayerInputs();
		PlaceCursorBlocks();

		transform.Rotate(Vector3.up * mouseHorizontal * 2.5f);
		cam.Rotate(Vector3.right * -mouseVertical * 2.5f);
	}

	private void Jump() 
	{
		verticalMomentum = jumpForce;
		IsGrounded = false;
		jumpRequest = false;
	}

	private void CalculateVelocity()
	{
		if (verticalMomentum > gravity)
			verticalMomentum += Time.fixedDeltaTime * gravity;

		if (IsSprinting)
			velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * sprintSpeed;
		else
			velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * walkSpeed;

		velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

		if ((velocity.z > 0 && Front) || (velocity.z < 0 && Back))
			velocity.z = 0;
		if ((velocity.x > 0 && Right) || (velocity.x < 0 && Left))
			velocity.x = 0;

		if (velocity.y < 0)
			velocity.y = CheckDownSpeed(velocity.y);
		else if (velocity.y > 0)
			velocity.y = CheckUpSpeed(velocity.y);
	}

	private void GetPlayerInputs()
	{
		horizontal = Input.GetAxis("Horizontal");
		vertical = Input.GetAxis("Vertical");
		mouseHorizontal = Input.GetAxis("Mouse X");
		mouseVertical = Input.GetAxis("Mouse Y");

		if (Input.GetButtonDown("Sprint")) IsSprinting = true;

		if (Input.GetButtonUp("Sprint")) IsSprinting = false;

		if (IsGrounded && Input.GetButtonDown("Jump")) jumpRequest = true;

		if(highlightBlock.gameObject.activeSelf)
		{
			if(Input.GetMouseButtonDown(0))
			{
				world.GetChunkFromVector3(highlightBlock.position).EditVoxel(highlightBlock.position, 0);
			}

			if (Input.GetMouseButtonDown(1))
			{
				world.GetChunkFromVector3(placeBlock.position).EditVoxel(placeBlock.position, SelectedBlockIndex);
			}
		}
	}

	private void PlaceCursorBlocks()
	{
		float _step = checkIncrement;
		Vector3 _lastPos = new Vector3();

		while(_step < reach)
		{
			Vector3 pos = cam.position + (cam.forward * _step);

			if(world.CheckForVoxel(pos))
			{
				highlightBlock.position = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
				placeBlock.position = _lastPos;

				highlightBlock.gameObject.SetActive(true);
				placeBlock.gameObject.SetActive(true);

				return;
			}

			_lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));

			_step += checkIncrement;
		}

		highlightBlock.gameObject.SetActive(false);
		placeBlock.gameObject.SetActive(false);
	}

	private float CheckDownSpeed(float downSpeed) 
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

	private float CheckUpSpeed(float upSpeed) 
	{
		if (
			world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth)) ||
			world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth))
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
		get {
			if (
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z + playerWidth)) ||
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth))
				)
				return true;
			else
				return false;
		}
	}

	public bool Back 
	{
		get {
			if (
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z - playerWidth)) ||
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth))
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
		get {
			if (
				world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y, transform.position.z)) ||
				world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z))
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
		get {
			if (
				world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y, transform.position.z)) ||
				world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z))
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
