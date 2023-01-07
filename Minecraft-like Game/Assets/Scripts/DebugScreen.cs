using TMPro;
using UnityEngine;

public class DebugScreen : MonoBehaviour
{
	private World world;
	private TextMeshProUGUI debugText;

	private float frameRate;
	private float timer;

	private int halfWorldSizeInVoxels;
	private int halfWorldSizeInChunks;

	private void Start()
	{
		world = GameObject.Find("World").GetComponent<World>();
		debugText = GetComponentInChildren<TextMeshProUGUI>();

		halfWorldSizeInVoxels = VoxelData.WorldSizeInVoxels / 2;
		halfWorldSizeInChunks = VoxelData.WorldSizeInChunks / 2;
	}

	private void Update()
	{
		string _debugText = "Minecraft-Like Game";
		_debugText += "\n";
		_debugText += frameRate + " FPS";
		_debugText += "\n";
		_debugText += "XYZ:" + (Mathf.FloorToInt(world.Player.transform.position.x) - halfWorldSizeInVoxels) + " / " + Mathf.FloorToInt(world.Player.transform.position.y) + " / " + (Mathf.FloorToInt(world.Player.transform.position.z) - halfWorldSizeInVoxels);
		_debugText += "\n";
		_debugText += "Chunk: " + (world.PlayerChunkCoord.x - halfWorldSizeInChunks) + " / " + (world.PlayerChunkCoord.z - halfWorldSizeInChunks);

		debugText.text = _debugText;

		if(timer > 1)
		{
			frameRate = (int)(1 / Time.unscaledDeltaTime);
			timer = 0;
		}
		else
		{
			timer += Time.deltaTime;
		}
	}
}
