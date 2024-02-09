using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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