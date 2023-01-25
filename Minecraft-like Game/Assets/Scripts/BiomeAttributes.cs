using UnityEngine;

[CreateAssetMenu(fileName = "BiomeAttributes", menuName = "MinecraftTutorial/Biome Attribute")]
public class BiomeAttributes : ScriptableObject 
{
	[Header("Biome Data")]
	public string BiomeAttributesName;
	public int SolidGroundHeight;
	public int TerrainHeight;
	public float TerrainScale;

	[Header("Tree Data")]
	public float TreeZoneScale = 1.3f;
	[Range(0.1f, 1f)]
	public float TreeZoneThreshold = 0.6f;
	public float TreePlacementScale = 15f;
	[Range(0.1f, 1f)]
	public float TreePlacementThreshold = 0.8f;

	public int MaxTreeSize = 16;
	public int MinTreeSize = 6;

	public Lode[] Lodes;
}

[System.Serializable]
public class Lode 
{
	public string NodeName;
	public byte BlockID;
	public int MinHeight;
	public int MaxHeight;
	public float Scale;
	public float Threshold;
	public float NoiseOffset;
}
