using Unity.Mathematics;
using UnityEngine;
using static PatataGames.GameSettings;
using static UnityEditor.PlayerSettings;

namespace PatataGames
{
	public static class Utils
	{
		public static int3 GetChunkCoordFromGlobalPosition(float3 position)
		{
			int3 chunkCoord = (int3)math.floor(position / WorldManager.Instance.ChunkSize);
			return chunkCoord;
		}

		public static int3 VoxelPositionFromGlobalPosition(Vector3 pos, int3 chunkPosition)
		{
			var check = math.floor(pos);

			var voxelPos = (int3)check - chunkPosition;
			return voxelPos;
		}

		public static int3 VoxelPositionToGlobalPosition(int3 voxelPosition, int3 chunkPosition)
		{
			return voxelPosition + chunkPosition;
		}

		public static int Int3ToInt(this int3 pos) => pos.x + pos.y* ChunkSize + pos.z* ChunkSize * ChunkSize;

		public static int3 IntToInt3(this int pos)
		{
			var x = pos % ChunkSize;
			var y = (pos / ChunkSize) % ChunkSize;
			var z = pos / (ChunkSize * ChunkSize);

			return new int3(x, y, z);
		}
	}
}
