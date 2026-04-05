using _Project.WorldGeneration.Blocks;
using Unity.Collections;
using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	/// <summary>
	///     Global terrain + noise settings.  Populated once by WorldAuthoring on startup.
	/// </summary>
	public struct WorldSettingsSingleton : IComponentData
	{
		public int Seed;
		public NativeCurve BiomeHeightCurve;
		public NativeCurve ErosionCurve;
		public NativeCurve PeaksAndValleysCurve;
		
		/// <summary>FastNoise2 encoded node tree string (copy from the FastNoise2 editor window).</summary>
		public FixedString512Bytes EncodedNodeTree;
	}

	/// <summary>
	///     All block prototypes indexed by block ID.
	///     Index 0 = air (zero-initialised Block).
	/// </summary>
	public struct WorldBlockRegistrySingleton : IComponentData
	{
		public NativeArray<Block>               Blocks;
		public NativeArray<NativeVoxelMeshData> Meshes;
	}
}