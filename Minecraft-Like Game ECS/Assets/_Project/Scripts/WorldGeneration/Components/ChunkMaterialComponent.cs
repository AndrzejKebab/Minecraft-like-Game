using Unity.Entities;
using UnityEngine;

namespace _Project.WorldGeneration.Components
{
	/// <summary>
	///     Unmanaged component referencing the chunk materials via UnityObjectRef
	///     (managed IComponentData classes are deprecated since Entities 6.6).
	/// </summary>
	public struct ChunkMaterialComponent : IComponentData
	{
		public UnityObjectRef<Material> SolidMaterial;
		public UnityObjectRef<Material> WaterMaterial;
	}
}
