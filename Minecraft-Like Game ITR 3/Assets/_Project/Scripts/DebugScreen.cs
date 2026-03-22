using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
	[Header("Sliders")] [SerializeField] private Slider viewDistance;

	[Header("Slider Text")] [SerializeField]
	private TextMeshProUGUI viewDistanceText;

	[Header("DebugText")] [SerializeField] private TextMeshProUGUI coordsText;
	private                                        int3            chunkCoords;
	private                                        float3          playerCoords;

	private World world;

	private void Awake()
	{
		world                          = GameObject.Find("World").GetComponent<World>();
		VoxelData.ViewDistanceInChunks = (byte)PlayerPrefs.GetInt("ViewDist", 8);
		viewDistance.value             = PlayerPrefs.GetInt("ViewDist", 8);
		viewDistanceText.text          = "View Distance: " + viewDistance.value;
	}

	public void Update()
	{
		Vector3 position = world.PlayerTransform.position;
		playerCoords = math.float3(position.x, position.y, position.z);
		playerCoords = math.floor(playerCoords);
		chunkCoords  = world.PlayerChunkCoord;
		coordsText.text =
			$"Coord: {playerCoords.x} / {playerCoords.y} / {playerCoords.z} <br>Chunk: {chunkCoords.x} / {chunkCoords.y} / {chunkCoords.z}";
	}

	public void ChangeViewDistance(Slider slider)
	{
		if (slider.name != "ViewDistSlider") return;
		PlayerPrefs.SetInt("ViewDist", (int)viewDistance.value);
		VoxelData.ViewDistanceInChunks = (byte)viewDistance.value;
		viewDistanceText.text          = "View Distance: " + viewDistance.value;
	}
}