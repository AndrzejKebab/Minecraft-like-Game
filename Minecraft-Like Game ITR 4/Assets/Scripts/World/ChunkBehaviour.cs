using UnityEngine;

namespace PatataStudio.World
{

	[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
	public class ChunkBehaviour : MonoBehaviour
	{

		private MeshRenderer Renderer;

		public Mesh Mesh { get; private set; }
		public MeshCollider Collider { get; private set; }

		private void Awake()
		{
			Mesh = GetComponent<MeshFilter>().mesh;
			Renderer = GetComponent<MeshRenderer>();
		}

		public void Init(MeshCollider m_collider)
		{
			Renderer.sharedMaterials = WorldManager.Instance.VoxelMaterials;
			Collider = m_collider;
		}
	}

}