using UnityEngine;

[CreateAssetMenu(menuName = "Voxels", fileName = "New Voxel Data")]
public class VoxelMeshData : ScriptableObject
{
    public VertexData[] VertexDatas;
}

[System.Serializable]
public struct VertexData
{
    public Vector3 Position;
    public Vector2 UV;
    public Vector3 Normal;
}