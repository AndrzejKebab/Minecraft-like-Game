using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct ThirdPersonPlayer : IComponentData
{
	[NonSerialized] public Entity ControlledCharacter;
	[NonSerialized] public Entity ControlledCamera;
}

[Serializable]
public struct ThirdPersonPlayerInputs : IComponentData
{
	public float2 MoveInput;
	public float2 CameraLookInput;
	public float  CameraZoomInput;

	[NonSerialized] public FixedInputEvent JumpPressed;
}