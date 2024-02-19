using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class PlayerBlocks : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI selectedBlockText;
	[SerializeField] private Transform highlightBlock;
	[SerializeField] private Transform placeHighlightBlock;
	[SerializeField]private Camera cam;
	[SerializeField]private World world;
	[SerializeField] private int reach;
	private ushort selectedBlockIndex = 1;

	[SerializeField] private LayerMask chunkMask = 6;
	private Ray ray;
	private RaycastHit hit;

	private void Awake()
	{
		selectedBlockText.text = $"Selected Block: {world.BlockTypes[selectedBlockIndex].name}";
	}

	private void Update()
	{
		PlaceCursorBlocks();
		EditBlock();
	}

	private void EditBlock()
	{
		var scroll = Input.GetAxis("Mouse ScrollWheel");

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

		selectedBlockText.text = $"Selected Block: {world.BlockTypes[selectedBlockIndex].name}";

		if (!highlightBlock.gameObject.activeSelf) return;
		if (Input.GetMouseButtonDown(0))
		{
			var position = highlightBlock.position;
			world.GetChunkFromVector3(position).EditVoxel(new int3(position), 0);
		}
		if (Input.GetMouseButtonDown(1))
		{
			var position = placeHighlightBlock.position;
			world.GetChunkFromVector3(position).EditVoxel(new int3(position), selectedBlockIndex);
		}
	}

	private void PlaceCursorBlocks()
	{
		ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

		if (Physics.Raycast(ray, out hit, reach, chunkMask))
		{
			Debug.DrawLine(ray.origin, hit.point, Color.red);
			var desiredPoint = hit.point - (hit.normal / 2);

			var gridIndex = new Vector3
			(
				Mathf.FloorToInt(desiredPoint.x),
				Mathf.FloorToInt(desiredPoint.y),
				Mathf.FloorToInt(desiredPoint.z)
			);

			highlightBlock.gameObject.SetActive(true);
			highlightBlock.position = gridIndex;

			var sideDesiredPoint = hit.point + (hit.normal / 2);

			var sideGridIndex = new Vector3
			(
				Mathf.FloorToInt(sideDesiredPoint.x),
				Mathf.FloorToInt(sideDesiredPoint.y),
				Mathf.FloorToInt(sideDesiredPoint.z)
			);

			placeHighlightBlock.gameObject.SetActive(true);
			placeHighlightBlock.position = sideGridIndex;
		}
		else
		{
			placeHighlightBlock.gameObject.SetActive(false);
			highlightBlock.gameObject.SetActive(false);
			Debug.DrawRay(cam.transform.position, cam.transform.forward * reach, Color.yellow);
		}
	}
}