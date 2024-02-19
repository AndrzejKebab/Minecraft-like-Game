using PatataStudio.World.Voxels;
using UnityEngine;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio.World
{
    public class WorldManager : Singleton<WorldManager>
    {
        [field:SerializeField] public VoxelData[] VoxelData { get; private set; }
        public ChunkManager ChunkManager { get; }
    }
}