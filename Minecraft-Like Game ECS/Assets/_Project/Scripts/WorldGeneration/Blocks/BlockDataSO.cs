using System;
using UnityEngine;

namespace _Project.WorldGeneration.Blocks
{
	// Scriptable Object for easy managing and creating blocks 
	[CreateAssetMenu(fileName = "BlockData", menuName = "Blocks/BlockData", order = 1)]
	public class BlockDataSo : ScriptableObject
	{
		public string               BlockName;
		public Block                Block;
		public MeshDataSO           VoxelData;
		public BlockTexturesLayer[] TexturesLayer;
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
		AO,
		Overlay
	}
}