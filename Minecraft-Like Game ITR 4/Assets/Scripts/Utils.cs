using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PatataStudio
{
    public static class Utils
    {
        private static readonly int3 chunkSize = GameSettings.ChunkSize;
        public static int3 MemberMultiply(this int3 a, int x, int y, int z) => new(a.x * x, a.y * y, a.z * z);
        public static int3 GetChunkCoords(Vector3 position) => GetChunkCoords(Vector3Int.FloorToInt(position));
        public static int3 GetChunkCoords(Vector3Int position) => GetChunkCoords(new int3(position.x, position.y, position.z));

        public static int3 GetChunkCoords(int3 position)
        {
            var modX = position.x % chunkSize.x;
            var modY = position.y % chunkSize.y;
            var modZ = position.z % chunkSize.z;

            var x = position.x - modX;
            var y = position.y - modY;
            var z = position.z - modZ;

            x = position.x < 0 && modX != 0 ? x - chunkSize.x : x;
            y = position.y < 0 && modY != 0 ? y - chunkSize.y : y;
            z = position.z < 0 && modZ != 0 ? z - chunkSize.z : z;

            return new int3(x, y, z);
        }

        public static int3 GetBlockIndex(Vector3 position) => GetBlockIndex(Vector3Int.FloorToInt(position));

        public static int3 GetBlockIndex(Vector3Int position)
        {
            var chunkCoords = GetChunkCoords(position);

            return new int3(position.x - chunkCoords.x, position.y - chunkCoords.y, position.z - chunkCoords.z);
        }

        public static readonly int3[] Directions =
        {
            new(1, 0, 0),
            new(-1, 0, 0),

            new(0, 1, 0),
            new(0, -1, 0),

            new(0, 0, 1),
            new(0, 0, -1)
        };
    }
}