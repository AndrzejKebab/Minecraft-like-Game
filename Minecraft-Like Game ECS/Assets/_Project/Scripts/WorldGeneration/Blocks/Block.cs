using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using UnityEngine;

namespace _Project.WorldGeneration.Blocks
{
	[Serializable]
	[BurstCompile]
	public struct Block
	{
		public                               ushort             ID;
		public                               ushort             MeshID;
		[MarshalAs(UnmanagedType.U1)] 
		public bool               IsTransparent;
		[MarshalAs(UnmanagedType.U1)] 
		public bool               IsFluid;
		public                               Color32            TintColor;
		public                               BlockDirectionType DirectionType;

		public NativeTexturesIDLayer BaseTextures;
		public NativeTexturesIDLayer NormalTextures;
		public NativeTexturesIDLayer SpecularTextures;
		public NativeTexturesIDLayer OverlayTextures;
	}
}