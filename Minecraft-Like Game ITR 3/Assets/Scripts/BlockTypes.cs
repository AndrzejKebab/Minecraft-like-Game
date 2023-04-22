using UnityEngine;

[CreateAssetMenu(fileName = "New BlockType", menuName = "BlockTypes")]
public class BlockTypes : ScriptableObject
{
	[Header("Block Data")]
	public string BlockName;
	public BlockTypesJob BlockTypeData;
}

[System.Serializable]
public struct BlockTypesJob
{
	public ushort BlockID;
	public bool IsSolid;
	public Textures TexturesIndex;

	[System.Serializable]
	public struct Textures
	{
		public short BackFaceTexture;
		public short FrontFaceTexture;
		public short TopFaceTexture;
		public short BottomFaceTexture;
		public short LeftFaceTexture;
		public short RightFaceTexture;
	};

	public int GetTexture2D(int faceIndex)
	{
		switch (faceIndex)
		{
			case 0:
				return TexturesIndex.BackFaceTexture;
			case 1:
				return TexturesIndex.FrontFaceTexture;
			case 2:
				return TexturesIndex.TopFaceTexture;
			case 3:
				return TexturesIndex.BottomFaceTexture;
			case 4:
				return TexturesIndex.LeftFaceTexture;
			case 5:
				return TexturesIndex.RightFaceTexture;
			default:
				Debug.LogError("GetTexture2D: Wrong Face Index!");
				return 0;
		}
	}
}