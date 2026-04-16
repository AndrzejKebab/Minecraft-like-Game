using System.Runtime.InteropServices;
using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	public struct PlayerInteractionState : IComponentData
	{
		[MarshalAs(UnmanagedType.U1)]
		public bool   BreakPressed;
		[MarshalAs(UnmanagedType.U1)]
		public bool   PlacePressed;
		public                               float  ScrollDelta;
		public                               ushort SelectedBlockID;
	}
}