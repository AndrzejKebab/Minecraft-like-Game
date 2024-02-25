using System;
using UnityEngine;

namespace PatataStudio.World.Voxels
{
    [CreateAssetMenu(menuName = "Voxel/New Voxel", fileName = "New Voxel Data")]
    public class VoxelData : ScriptableObject
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
            Voxel.FaceDatas = VoxelMeshData.FaceDatas;
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