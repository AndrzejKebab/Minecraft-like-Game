using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;

[Serializable]
public struct FirstPersonPlayer : IComponentData
{
	[NonSerialized] public Entity ControlledCharacter;

	[FormerlySerializedAs("LookRotationSpeed")]
	public float LookInputSensitivity;
}

[Serializable]
public struct FirstPersonPlayerInputs : IComponentData
{
	public float2 MoveInput;
	public float2 LookInput;

	[NonSerialized] public FixedInputEvent JumpPressed;
}