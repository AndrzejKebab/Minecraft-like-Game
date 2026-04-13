using _Project.UI;
using _Project.WorldGeneration.Components;
using Unity.Entities;

namespace _Project.Character
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class PlayerUIUpdateSystem : SystemBase
	{
		private ushort lastSelectedBlockID = ushort.MaxValue;

		protected override void OnCreate()
		{
			RequireForUpdate<WorldBlockRegistrySingleton>();
			RequireForUpdate<PlayerInteractionState>();
		}

		protected override void OnUpdate()
		{
			if (PlayerUIController.Instance == null) return;

			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();

			foreach (RefRO<PlayerInteractionState> interactState in SystemAPI.Query<RefRO<PlayerInteractionState>>())
			{
				var currentID = interactState.ValueRO.SelectedBlockID;

				if (currentID == lastSelectedBlockID) continue;
				lastSelectedBlockID = currentID;

				if (currentID >= registry.Blocks.Length) continue;
				var blockName = registry.BlockNames[currentID].ToString();
                        
				PlayerUIController.Instance.UpdateSelectedBlockText(blockName);
			}
		}
	}
}