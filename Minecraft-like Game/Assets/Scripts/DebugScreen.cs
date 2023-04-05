using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
	[Header("Sliders")]
	[SerializeField] private Slider viewDistance;
	//[SerializeField] private Slider musicVolume;

	[Header("Slider Text")]
	[SerializeField] private TextMeshProUGUI viewDistanceText;
	//[SerializeField] private TextMeshProUGUI musicVolumeText;

	[Header("DebugText")]
	[SerializeField] private TextMeshProUGUI coordsText;

	//[Header("Mixers")]
	//[SerializeField] private AudioMixer masterMixer;

	private World world;
	[SerializeField] private Transform player;
	private float3 playerCoords;
	private int2 chunkCoords;

	private void Awake()
	{
		world = GameObject.Find("World").GetComponent<World>();
		VoxelData.ViewDistanceInChunks = (byte)PlayerPrefs.GetInt("ViewDist", 16);
		viewDistance.value = PlayerPrefs.GetInt("ViewDist", 16);
		viewDistanceText.text = "View Distance: " + viewDistance.value;

		//musicVolume.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
		//musicVolumeText.text = "Music Volume: " + Mathf.Round(musicVolume.value * 100) + "%";
	}

	public void Update()
	{
		playerCoords = math.float3(world.playerTransform.position.x, world.playerTransform.position.y, world.playerTransform.position.z);
		playerCoords = math.floor(playerCoords);
		chunkCoords = world.playerChunkCoord;
		coordsText.text = $"Coord: {playerCoords.x} / {playerCoords.y} / {playerCoords.z} <br>Chunk: {chunkCoords.x} / {chunkCoords.y}";
	}

	public void ChangeVolume(Slider slider)
	{
		if (slider.name == "ViewDistSlider")
		{
			PlayerPrefs.SetInt("ViewDist", (int)viewDistance.value);
			VoxelData.ViewDistanceInChunks = (byte)viewDistance.value;
			viewDistanceText.text = "View Distance: " + viewDistance.value;
		}
		//else if (slider.name == "MusicVolume")
		//{
		//	masterMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolume.value) * 20);
		//	PlayerPrefs.SetFloat("MusicVolume", musicVolume.value);
		//	musicVolumeText.text = "Music Volume: " + Mathf.Round(musicVolume.value * 100) + "%";
		//}
	}
}