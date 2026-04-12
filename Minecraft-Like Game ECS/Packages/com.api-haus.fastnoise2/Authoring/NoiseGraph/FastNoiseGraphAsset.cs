using UnityEngine;

namespace FastNoise2.Authoring.NoiseGraph
{
	[CreateAssetMenu(fileName = "FastNoise Graph", menuName = "FastNoise2/Graph Asset")]
	public class FastNoiseGraphAsset : ScriptableObject
	{
		[SerializeField]
		public FastNoiseGraph savedGraph;
	}
}
