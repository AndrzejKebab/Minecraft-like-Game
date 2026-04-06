using _Project.UI;
using _Project.WorldGeneration.Components;
using Unity.Entities;

namespace _Project.WorldGeneration.Systems
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
			// Wait until the UI Controller is awake in the scene
			if (PlayerUIController.Instance == null) return;

			var registry = SystemAPI.GetSingleton<WorldBlockRegistrySingleton>();

			// Query the player's interaction state
			foreach (var interactState in SystemAPI.Query<RefRO<PlayerInteractionState>>())
			{
				ushort currentID = interactState.ValueRO.SelectedBlockID;

				// Only update the UI Toolkit if the ID actually changed!
				if (currentID == lastSelectedBlockID) continue;
				lastSelectedBlockID = currentID;

				// Safety check to ensure the ID is within the registry bounds
				if (currentID >= registry.Blocks.Length) continue;
				// Convert the native FixedString to a standard C# string
				string blockName = registry.Blocks[currentID].Name.ToString();
                        
				// Push to the UI Manager
				PlayerUIController.Instance.UpdateSelectedBlockText(blockName);
			}
		}
	}
}