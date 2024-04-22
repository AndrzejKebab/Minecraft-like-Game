using PatataStudio.World.Voxels;
using Sirenix.Serialization;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataStudio
{
    public class GameSettings : Singleton<GameSettings>
    {
        #region Player Settings
        public static int ViewDistance = 8;
        #endregion

        #region World Settings
        public static readonly int ChunkSize = 32;
        public const int WorldHeight = 384;
        public const int WaterHeight = 100;
        public const int WorldSizeInChunks = 1875000;
        public static int WorldSizeInVoxels => WorldSizeInChunks * ChunkSize;

        [OdinSerialize] public HeightMapData[] heightMapDatas;
        [OdinSerialize] public VoxelData[] voxelDataDatas;
        #endregion

        public const byte TextureAtlasSizeInBlocks = 16;
        public static float NormalizedBlockTextureSize => 1f / TextureAtlasSizeInBlocks;
    }
}