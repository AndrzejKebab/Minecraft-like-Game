using System;
using Unity.Burst;

namespace _Project.WorldGeneration.Blocks
{
	[BurstCompile]
	[Serializable]
	public struct Block
	{
		public ushort ID;
		public ushort MeshID;
		public bool   IsTransparent;
		public bool   IsFluid;
		public bool   IsSolid;

		public NativeTexturesIDLayer BaseTextures;
		public NativeTexturesIDLayer OverlayTextures;
	}
}