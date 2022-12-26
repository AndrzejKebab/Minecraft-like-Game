using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Biome Attributes", menuName = "Minecraft/Biomes")]
public class BiomeAttributes : ScriptableObject
{
	public string BiomeName;
	public int SolidGroundHeight;
	public int TerrainHeight;
	public float TerrainScale;

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