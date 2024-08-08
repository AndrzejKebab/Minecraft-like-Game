using System;
using UnityEngine;
using UtilityLibrary.Core;

namespace PatataStudio
{
	[CreateAssetMenu(menuName = "Minecraft/Voxel/Voxel Type", fileName = "New Voxel Type", order = 0)]
	public class VoxelType : ScriptableObject
	{
		[Header("Voxel Data")]
		[field: SerializeField] public string VoxelName { get; private set; }
		[field: SerializeField] private Voxel voxel;
		[field: SerializeField] private VoxelMeshData VoxelMeshData;

		[Header("Texture Indexes")]
		public int FrontFaceTexture;
		public int BackFaceTexture;
		public int RightFaceTexture;
		public int LeftFaceTexture;
		public int TopFaceTexture;
		public int BottomFaceTexture;

		public int GetTextureID(int faceIndex)
		{
			switch (faceIndex)
			{
				case 0:
					return FrontFaceTexture;
				case 1:
					return BackFaceTexture;
				case 2:
					return RightFaceTexture;
				case 3:
					return LeftFaceTexture;
				case 4:
					return TopFaceTexture;
				case 5:
					return BottomFaceTexture;
				default:
					Debug.Log("Error in GetTextureID; invalid face index");
					return 0;
			}
		}

		public int GetTextureID(FaceSide faceSide)
		{
			switch (faceSide)
			{
				case FaceSide.Front:
					return FrontFaceTexture;
				case FaceSide.Back:
					return BackFaceTexture;
				case FaceSide.Right:
					return RightFaceTexture;
				case FaceSide.Left:
					return LeftFaceTexture;
				case FaceSide.Top:
					return TopFaceTexture;
				case FaceSide.Bottom:
					return BottomFaceTexture;
				default:
					Debug.Log("Error in GetTextureID; invalid face index");
					return 0;
			}
		}

		public Voxel GetVoxel()
		{
			return voxel;
		}

		private void OnValidate()
		{
			if (VoxelMeshData != null) voxel.FaceDatas = VoxelMeshData.FaceDatas;
		}
	}

	[Serializable]
	public struct Voxel
	{
		[field: SerializeField] public ushort VoxelID { get; private set; }
		[field: SerializeField] public bool IsSolid { get; private set; }
		[field: SerializeField] public bool IsTransparent { get; private set; }
		[field: SerializeField] public bool IsFluid { get; private set; }
		private FaceData[] faceDatas;
		public FaceData[] FaceDatas
		{
			get
			{
				return faceDatas;
			}
			set
			{
				if (faceDatas.IsNullOrEmpty<FaceData>()) faceDatas = value;
			}
		}
	}
}