using Unity.Mathematics;
using UnityEngine;
using UtilityLibrary.Unity.Runtime.Patterns;

namespace PatataGames
{
	public class WorldManager : Singleton<WorldManager>
	{
		[Header("World Settings")]
		[field: SerializeField] public int Seed { get; private set; }

		[Header("Chunk Settings")] 
		public int3 ChunkSize { get; private set; } = new(GameSettings.ChunkSize);

		[Header("Voxel Settings")]
		[field: SerializeField] public Material[] Materials { get; private set; }
	}
}