using UnityEngine;
using _Project.WorldGeneration.Blocks;

namespace _Project
{
	public class GameDatabase : MonoBehaviour
	{
		public static GameDatabase Instance { get; private set; }

		[Header("Block Registry")]
		public BlockDataSo[] AllBlocks;[Header("Materials")]
		public Material ChunkMaterial;

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else Destroy(gameObject);
		}
	}
}