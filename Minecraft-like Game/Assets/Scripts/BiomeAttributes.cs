using UnityEngine;

[CreateAssetMenu(fileName = "Biome", menuName = "Biome Atrribute")]
public class BiomeAttributes : ScriptableObject
{
	public string BiomeName;
	public int SolidGroundHeight;
	public int BiomeHeight;
	public int BiomeScale;
}
