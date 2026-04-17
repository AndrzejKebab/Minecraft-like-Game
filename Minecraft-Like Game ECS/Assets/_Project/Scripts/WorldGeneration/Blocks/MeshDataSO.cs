using UnityEngine;
using UnityEngine.Serialization;

namespace _Project.WorldGeneration.Blocks
{
	[CreateAssetMenu(fileName = "MeshDataSO", menuName = "Blocks/MeshDataSO", order = 1)]
	public class MeshDataSO : ScriptableObject
	{
		[FormerlySerializedAs("meshData")] 
		[SerializeField] public VoxelMeshData MeshData;
	}
}