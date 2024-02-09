using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class PlayerBlocks : MonoBehaviour
{
	[SerializeField] private Transform highlightBlock;
	[SerializeField] private Transform placeBlock;
	[SerializeField] private int reach;
	[SerializeField] private TextMeshProUGUI selectedBlockText;
	private ushort selectedBlockIndex = 1;
	private Camera cam;
	private World world;

	[SerializeField] private LayerMask chunkMask = 6;
	private Ray ray;
	private RaycastHit hit;

	private void Awake()
	{
		highlightBlock = GameObject.Find("HiglightBlock").GetComponent<Transform>();
		placeBlock = GameObject.Find("PlaceHighlightBlock").GetComponent<Transform>();
		world = GameObject.Find("World").GetComponent<World>();
		cam = Camera.main;
		selectedBlockText = GameObject.Find("ToolBar").GetComponent<TextMeshProUGUI>();

		selectedBlockText.text = $"Seleceted Block: {world.BlockTypes[selectedBlockIndex].name}";
	}

	void Update()
	{
		PlaceCursorBlocks();
		EditBlock();
	}

	private void EditBlock()
	{
		float scroll = Input.GetAxis("Mouse ScrollWheel");

		if (scroll != 0)
		{
			if (scroll > 0)
			{
				selectedBlockIndex++;
			}
			else
			{
				selectedBlockIndex--;
			}
		}

		if (selectedBlockIndex > (ushort)world.BlockTypesJobs.Length - 1)
		{
			selectedBlockIndex = 1;
		}
		if (selectedBlockIndex < 1)
		{
			selectedBlockIndex = (ushort)(world.BlockTypesJobs.Length - 1);
		}

		selectedBlockText.text = $"Seleceted Block: {world.BlockTypes[selectedBlockIndex].name}";

		if (highlightBlock.gameObject.activeSelf)
		{
			if (Input.GetMouseButtonDown(0))
			{
				world.GetChunkFromVector3(highlightBlock.position).EditVoxel(new int3(highlightBlock.position), 0);
			}
			if (Input.GetMouseButtonDown(1))
			{
				world.GetChunkFromVector3(placeBlock.position).EditVoxel(new int3(placeBlock.position), selectedBlockIndex);
			}
		}
	}

	private void PlaceCursorBlocks()
	{
		ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

		if (Physics.Raycast(ray, out hit, reach, chunkMask))
		{
			Debug.DrawLine(ray.origin, hit.point, Color.red);
			Vector3 desiredPoint = hit.point - (hit.normal / 2);

			Vector3 gridIndex = new Vector3
			(
				Mathf.FloorToInt(desiredPoint.x),
				Mathf.FloorToInt(desiredPoint.y),
				Mathf.FloorToInt(desiredPoint.z)
			);

			highlightBlock.gameObject.SetActive(true);
			highlightBlock.position = gridIndex;

			Vector3 sideDesiredPoint = hit.point + (hit.normal / 2);

			Vector3 sideGridIndex = new Vector3
			(
				Mathf.FloorToInt(sideDesiredPoint.x),
				Mathf.FloorToInt(sideDesiredPoint.y),
				Mathf.FloorToInt(sideDesiredPoint.z)
			);

			placeBlock.gameObject.SetActive(true);
			placeBlock.position = sideGridIndex;
		}
		else
		{
			placeBlock.gameObject.SetActive(false);
			highlightBlock.gameObject.SetActive(false);
			Debug.DrawRay(cam.transform.position, cam.transform.forward * reach, Color.yellow);
		}
	}
}