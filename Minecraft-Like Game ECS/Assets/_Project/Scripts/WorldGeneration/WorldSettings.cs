using System.Collections.Generic;
using System.Linq;
using _Project.WorldGeneration.Blocks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project.WorldGeneration
{
	public class WorldSettings : MonoBehaviour
	{
		[Header("World Settings")] public int Seed             = 1337;
		public                            int MaxTerrainHeight = 192;

		[Header("FastNoise2 Settings")] public string EncodedNodeTree =
			"E@BBZEG@BD8JFgokCMP1KD8JLgAB@BCQ0ABw@BgAACBACQc@BWRBA9Cle/GGZmZj8EA5qZGT8LAACAPxwDAABwQgQ=";

		public AnimationCurve BiomeHeightCurve;
		public AnimationCurve ErosionCurve;
		public AnimationCurve PeaksAndValleysCurve;

		[Header("Blocks Settings")]
		[Tooltip("Automatically finds all BlockDataSo assets in the project and sorts them by ID.")]
		public bool AutoCollectBlocks = true;

		public BlockDataSo[] BlockDataSos;

		[Header("Rendering")] public Material ChunkMaterial;

		public static WorldSettings Instance { get; private set; }

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else Destroy(gameObject);
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (AutoCollectBlocks)
			{
				EditorApplication.delayCall += CollectAndSortBlocks;
			}
		}

		[ContextMenu("Force Collect And Sort Blocks")]
		public void CollectAndSortBlocks()
		{
			if (this == null) return;

			var guids = AssetDatabase.FindAssets("t:BlockDataSo");
			List<BlockDataSo> list = guids.Select(AssetDatabase.GUIDToAssetPath)
			                              .Select(AssetDatabase.LoadAssetAtPath<BlockDataSo>).Where(so => so != null)
			                              .ToList();

			// Sort by the Native Block ID
			list.Sort((a, b) => a.Block.ID.CompareTo(b.Block.ID));

			// Verify if changes actually occurred to prevent dirtying the scene unnecessarily
			var isDifferent = (BlockDataSos == null || BlockDataSos.Length != list.Count ||
			                   list.Where((t, i) => BlockDataSos[i] != t).Any());

			if (!isDifferent) return;
			BlockDataSos = list.ToArray();
			EditorUtility.SetDirty(this);
		}
#endif
	}
}