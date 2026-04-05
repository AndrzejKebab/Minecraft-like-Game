using System;
using Unity.Burst;

namespace _Project.WorldGeneration.Blocks
{
	[BurstCompile]
	[Serializable]
	public struct NativeTexturesIDLayer
	{
		public ushort Front;
		public ushort Back;
		public ushort Top;
		public ushort Bottom;
		public ushort Left;
		public ushort Right;

		public NativeTexturesIDLayer(ushort front, ushort back, ushort top, ushort bottom, ushort left, ushort right)
		{
			Front  = front;
			Back   = back;
			Top    = top;
			Bottom = bottom;
			Left   = left;
			Right  = right;
		}
	}
}