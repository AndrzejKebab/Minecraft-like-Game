namespace PatataStudio
{
	public static class GameSettings
	{
		#region Player
		public static byte ViewDistance = 4;
		#endregion

		#region Chunk
		public const int ChunkSize = 64;
		public const int MaxTerrainHeight = 512;
		#endregion

		#region Textures
		public const byte TextureAtlasSizeInBlocks = 16;
		public static float NormalizedBlockTextureSize => 1f / TextureAtlasSizeInBlocks;
		#endregion
	}
}