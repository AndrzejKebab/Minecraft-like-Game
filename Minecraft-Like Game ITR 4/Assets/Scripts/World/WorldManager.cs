using PatataStudio.World.Voxels;
using UnityEngine;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio.World
{
    public class WorldManager : Singleton<WorldManager>
    {
        [field:SerializeField] public Material[] VoxelMaterials { get; private set; }
        [field:SerializeField] public VoxelData[] VoxelData { get; private set; }
        public ChunkManager ChunkManager { get; } = new ();
		public ChunkScheduler ChunkScheduler { get; private set; }

		protected override void Awake()
		{
			base.Awake();
		}

		private void Start()
		{
			
		}

		private void Update()
		{
			
		}

		private void LateUpdate()
		{

		}

		private void OnDestroy()
		{
			ChunkManager.Dispose();
			ChunkScheduler.Dispose();
		}
	}
}