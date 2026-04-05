using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration.Components
{
	public class ChunkMaterialComponent : IComponentData
	{
		public Material Material;
	}
}