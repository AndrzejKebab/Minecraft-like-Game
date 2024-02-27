using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "NoiseData", menuName = "Noise", order = 1)]
public class HeightMapData : ScriptableObject
{
	[Header("Perlin Noise Data")]
	public NoiseData Noise;
}

[Serializable]
public struct NoiseData
{
	[Range(0, 10000)]
	public ushort NoiseScale;
	[Range(1, 8)]
	public byte Octaves;
	[Range(1f, 4f)]
	public float Lacunarity;
	[Range(0f, 1f)]
	public float Persistence;
}