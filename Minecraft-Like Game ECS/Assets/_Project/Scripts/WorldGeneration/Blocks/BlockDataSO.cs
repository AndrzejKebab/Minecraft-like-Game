using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project.WorldGeneration.Blocks
{
	[CreateAssetMenu(fileName = "BlockData", menuName = "Blocks/BlockData", order = 1)]
	public class BlockDataSo : ScriptableObject
	{
		public string               BlockName;
		public Block                Block;
		public Color32              TintColor = new(105, 112, 17, 255);
		public MeshDataSO           VoxelData;
		public BlockTexturesLayer[] TexturesLayer;

#if UNITY_EDITOR
		private void OnValidate()
		{
			EditorApplication.delayCall += AssignUniqueIdIfNeeded;

			Block.TintColor = TintColor;
		}

		private void AssignUniqueIdIfNeeded()
		{
			if (this == null) return;

			var guids      = AssetDatabase.FindAssets("t:BlockDataSo");
			var usedIds    = new HashSet<ushort>();
			var idConflict = false;

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var so   = AssetDatabase.LoadAssetAtPath<BlockDataSo>(path);

				if (so == null || so == this) continue;
				usedIds.Add(so.Block.ID);

				// If another asset already claims this ID, we have a duplication conflict!
				if (so.Block.ID == Block.ID && Block.ID != 0) idConflict = true;
			}

			// ID 0 is strictly reserved for Air. 
			var isAir = string.Equals(BlockName, "Air", StringComparison.OrdinalIgnoreCase);

			// Reassign if it's uniquely 0 (and not Air), or if the ID is conflicting with another block
			if ((Block.ID != 0 || isAir) && !idConflict) return;
			ushort newId = 1;

			// Find the lowest available ID
			while (usedIds.Contains(newId)) newId++;

			Block.ID = newId;
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssets();

			Debug.Log($"[Block Workflow] Auto-assigned unique ID {newId} to block '{BlockName ?? name}'.");
		}
#endif
	}

	[Serializable]
	public class BlockTexturesLayer
	{
		public TextureType TextureType;
		public Texture2D   FrontTexture;
		public Texture2D   BackTexture;
		public Texture2D   TopTexture;
		public Texture2D   BottomTexture;
		public Texture2D   RightTexture;
		public Texture2D   LeftTexture;
	}

	public enum TextureType
	{
		Base,
		Normal,
		Specular,
		Overlay
	}
}