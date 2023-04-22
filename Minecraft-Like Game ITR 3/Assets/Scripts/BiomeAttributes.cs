using UnityEngine;

[CreateAssetMenu(fileName = "New Biome Atrribute", menuName = "Biome Atrribute")]
public class BiomeAttributes : ScriptableObject
{
	public string BiomeName;
	public BiomeAttributesJob BiomeData;
}

[System.Serializable]
public struct BiomeAttributesJob
{
	public int SolidGroundHeight;
	public int BiomeHeight;
	public int BiomeScale;
}