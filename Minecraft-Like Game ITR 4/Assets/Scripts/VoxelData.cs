using UnityEngine;

[CreateAssetMenu(menuName = "Voxel/New Voxel", fileName = "New Voxel Data")]
public class VoxelData : ScriptableObject
{
    public string VoxelName;
    public ushort VoxelID;
    public bool IsSolid;
    public bool IsTransparent;
    public bool IsFluid;
    public VoxelMeshData VoxelMeshData;

    public Voxel GetVoxelData() => new(IsSolid, IsTransparent, IsFluid, VoxelMeshData.FaceDatas);
}

[System.Serializable]
public struct Voxel
{
    public bool IsSolid;
    public bool IsTransparent;
    public bool IsFluid;
    public FaceData[] FaceDatas;
    
    public Voxel(bool isSolid, bool isTransparent, bool isFluid, FaceData[] faceDatas)
    {
        IsSolid = isSolid;
        IsTransparent = isTransparent;
        IsFluid = isFluid;
        FaceDatas = faceDatas;
    }
}