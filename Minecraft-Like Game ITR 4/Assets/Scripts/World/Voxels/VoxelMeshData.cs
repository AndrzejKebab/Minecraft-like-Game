using System;
using UnityEngine;

namespace PatataStudio.World.Voxels
{
    [CreateAssetMenu(menuName = "Voxel/New Mesh Data", fileName = "New Voxel Mesh Data")]

    public class VoxelMeshData : ScriptableObject
    {
        public FaceData[] FaceDatas;
    }

    [Serializable]
    public struct FaceData
    {
        public VertexData[] Vertices;
    }

    [Serializable]
    public struct VertexData
    {
        public Vector3 Position;
        public Vector2 UV;
    }
}