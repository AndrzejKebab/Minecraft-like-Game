using FastNoise2.Authoring.NoiseGraph;
using Unity.Mathematics;
using UnityEngine;

namespace FastNoise2.Authoring.NoiseAsset
{
	public enum NoiseAssetTextureOutput
	{
		Texture2D,
		Texture3D,
	}

	public enum NoiseAssetTextureFormat
	{
		// R8,
		// R16,
		R32,
	}

	[CreateAssetMenu(fileName = "Noise Asset", menuName = "FastNoise2/Baked Noise")]
	public class BakedNoiseTextureAsset : ScriptableObject
	{
		public FastNoiseGraph graph;
		public NoiseAssetTextureOutput textureOutput = NoiseAssetTextureOutput.Texture3D;
		public NoiseAssetTextureFormat textureFormat = NoiseAssetTextureFormat.R32;
		public int3 resolution = 128;
		public int3 offset;
		public float frequency = .02f;
		public int seed = 1337;
	}
}
