using System.Collections.Generic;
using System.Linq;
using _Project.WorldGeneration.Blocks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project
{
	public class GameDatabase : MonoBehaviour
	{
		[Header("Block Registry")]
		[Tooltip("Automatically finds all BlockDataSo assets in the project and sorts them by ID.")]
		public bool AutoCollectBlocks = true;

		public BlockDataSo[] AllBlocks;

		[Header("Materials")] public Material ChunkMaterial;

		public        Material     WaterMaterial;
		public static GameDatabase Instance { get; private set; }

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else Destroy(gameObject);
		}

		// statics survive when domain reload is disabled — reset on play mode entry
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (AutoCollectBlocks) EditorApplication.delayCall += CollectAndSortBlocks;
		}

		private void OnDisable()
		{
			EditorApplication.delayCall -= CollectAndSortBlocks;
		}

		[ContextMenu("Force Collect And Sort Blocks")]
		public void CollectAndSortBlocks()
		{
			if (this == null) return;

			var guids = AssetDatabase.FindAssets("t:BlockDataSo");
			List<BlockDataSo> list = guids.Select(AssetDatabase.GUIDToAssetPath)
			                              .Select(AssetDatabase.LoadAssetAtPath<BlockDataSo>)
			                              .Where(so => so != null)
			                              .ToList();

			// Sort by the Native Block ID
			list.Sort((a, b) => a.Block.ID.CompareTo(b.Block.ID));

			// Verify if changes actually occurred to prevent dirtying the scene unnecessarily
			var isDifferent = AllBlocks == null || AllBlocks.Length != list.Count ||
			                  list.Where((t, i) => AllBlocks[i] != t).Any();

			if (!isDifferent) return;
			AllBlocks = list.ToArray();
			EditorUtility.SetDirty(this);
		}
#endif
	}
}