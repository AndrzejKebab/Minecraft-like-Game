using Unity.Entities;

namespace _Project.WorldGeneration.Components
{
	public struct PlayerInteractionState : IComponentData
	{
		public bool   BreakPressed;
		public bool   PlacePressed;
		public float  ScrollDelta;
		public ushort SelectedBlockID;
	}
}