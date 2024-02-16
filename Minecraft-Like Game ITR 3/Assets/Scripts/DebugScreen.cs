using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
    [Header("Sliders")] [SerializeField] private Slider viewDistance;
    //[SerializeField] private Slider musicVolume;

    [Header("Slider Text")] [SerializeField]
    private TextMeshProUGUI viewDistanceText;
    //[SerializeField] private TextMeshProUGUI musicVolumeText;

    [Header("DebugText")] [SerializeField] private TextMeshProUGUI coordsText;

    //[Header("Mixers")]
    //[SerializeField] private AudioMixer masterMixer;

    private World world;
    private float3 playerCoords;
    private int3 chunkCoords;

    private void Awake()
    {
        world = GameObject.Find("World").GetComponent<World>();
        VoxelData.ViewDistanceInChunks = (byte)PlayerPrefs.GetInt("ViewDist", 8);
        viewDistance.value = PlayerPrefs.GetInt("ViewDist", 8);
        viewDistanceText.text = "View Distance: " + viewDistance.value;

        //musicVolume.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        //musicVolumeText.text = "Music Volume: " + Mathf.Round(musicVolume.value * 100) + "%";
    }

    public void Update()
    {
        var position = world.PlayerTransform.position;
        playerCoords = math.float3(position.x, position.y, position.z);
        playerCoords = math.floor(playerCoords);
        chunkCoords = world.PlayerChunkCoord;
        coordsText.text =
            $"Coord: {playerCoords.x} / {playerCoords.y} / {playerCoords.z} <br>Chunk: {chunkCoords.x} / {chunkCoords.y} / {chunkCoords.z}";
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