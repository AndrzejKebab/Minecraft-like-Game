using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "VoxelGame/Mesh Type", fileName = "NewMeshType")]
public class MeshData : ScriptableObject
{
    public FaceData[] FaceDatas;

    [Button("Print Face Data")]
    private void PrintFaceData()
    {
        StringBuilder sb = new StringBuilder();
        byte count = 0;
        foreach (var faceData in FaceDatas)
        {
            sb.AppendLine($"Face Data {count}: ");
            sb.AppendLine(faceData.ToString());
            count++;
        }
        
        Debug.Log(sb.ToString());
    }
}