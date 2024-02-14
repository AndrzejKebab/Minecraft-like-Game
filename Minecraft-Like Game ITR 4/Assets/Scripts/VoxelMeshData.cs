using UnityEngine;

[CreateAssetMenu(menuName = "Voxel/New Mesh Data", fileName = "New Voxel Mesh Data")]
public class VoxelMeshData : ScriptableObject
{
    public FaceData[] FaceDatas;
}

[System.Serializable]
public struct FaceData
{
    public VertexData[] Vertices;
}

[System.Serializable]
public struct VertexData
{
    public Vector3 Position;
    public Vector2 UV;
}