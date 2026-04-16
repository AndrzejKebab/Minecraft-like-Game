using _Project.WorldGeneration.Components;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class FirstPersonPlayerAuthoring : MonoBehaviour
{
	public GameObject ControlledCharacter;
	public float      LookInputSensitivity = 0.2f;

	public class Baker : Baker<FirstPersonPlayerAuthoring>
	{
		public override void Bake(FirstPersonPlayerAuthoring authoring)
		{
			Entity entity = GetEntity(TransformUsageFlags.None);
			AddComponent(entity, new FirstPersonPlayer
			                     {
				                     ControlledCharacter =
					                     GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
				                     LookInputSensitivity = authoring.LookInputSensitivity
			                     });
			AddComponent<FirstPersonPlayerInputs>(entity);

			AddComponent(entity, new PlayerInteractionState { SelectedBlockID = 1 });
		}
	}
}