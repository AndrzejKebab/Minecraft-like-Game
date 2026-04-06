using _Project.WorldGeneration.Blocks;
using UnityEngine;

namespace _Project.WorldGeneration
{
	public class WorldSettings : MonoBehaviour
	{
		[Header("World Settings")] public int Seed = 1337;

		public int MaxTerrainHeight = 192;

		[Header("FastNoise2 Settings")] public string EncodedNodeTree =
			"E@BBZEG@BD8JFgokCMP1KD8JLgAB@BCQ0ABw@BgAACBACQc@BWRBA9Cle/GGZmZj8EA5qZGT8LAACAPxwDAABwQgQ=";

		public                             AnimationCurve BiomeHeightCurve;
		public                             AnimationCurve ErosionCurve;
		public                             AnimationCurve PeaksAndValleysCurve;
		[Header("Blocks Settings")] public BlockDataSo[]  BlockDataSos;

		[Header("Rendering")] public Material ChunkMaterial;

		public static WorldSettings Instance { get; private set; }

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else Destroy(gameObject);
		}
	}
}