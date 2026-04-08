using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.WorldGeneration.Blocks
{
	[Serializable]
	[BurstCompile]
	public struct Block
	{
		public ushort             ID;
		public ushort             MeshID;
		public bool               IsTransparent;
		public bool               IsFluid;
		public Color32            TintColor;
		public BlockDirectionType DirectionType;

		public FixedString32Bytes    Name;
		public NativeTexturesIDLayer BaseTextures;
		public NativeTexturesIDLayer NormalTextures;
		public NativeTexturesIDLayer SpecularTextures;
		public NativeTexturesIDLayer OverlayTextures;
	}
}