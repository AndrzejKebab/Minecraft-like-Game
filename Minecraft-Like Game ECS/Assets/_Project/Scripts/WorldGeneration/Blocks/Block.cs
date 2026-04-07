using System;
using Unity.Burst;
using Unity.Collections;

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

		public BlockDirectionType DirectionType;

		public FixedString32Bytes    Name;
		public NativeTexturesIDLayer BaseTextures;
		public NativeTexturesIDLayer NormalTextures;
		public NativeTexturesIDLayer SpecularTextures;
		public NativeTexturesIDLayer OverlayTextures;
	}
}