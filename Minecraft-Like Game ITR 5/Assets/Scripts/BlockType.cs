using System;
using UnityEngine;

namespace PatataStudio
{
	[CreateAssetMenu(menuName = "Minecraft/Voxel/Voxel Type", fileName = "New Voxel Type", order = 0)]
	public class VoxelType : ScriptableObject
	{
		public string VoxelName;
		public Voxel Voxel;
		public VoxelMeshData VoxelMeshData;

		public Voxel GetVoxel()
		{
			return Voxel;
		}

		private void OnValidate()
		{
			if (VoxelMeshData != null) Voxel.FaceDatas = VoxelMeshData.FaceDatas;
		}
	}

	[Serializable]
	public struct Voxel
	{
		public ushort VoxelID;
		public bool IsSolid;
		public bool IsTransparent;
		public bool IsFluid;
		[HideInInspector] public FaceData[] FaceDatas;
	}
}