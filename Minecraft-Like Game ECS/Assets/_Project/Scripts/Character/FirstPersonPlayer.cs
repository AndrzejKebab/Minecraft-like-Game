using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;

[Serializable]
public struct FirstPersonPlayer : IComponentData
{
    public Entity ControlledCharacter;
    [FormerlySerializedAs("LookRotationSpeed")] public float LookInputSensitivity;
}

[Serializable]
public struct FirstPersonPlayerInputs : IComponentData
{
    public float2 MoveInput;
    public float2 LookInput;
    public FixedInputEvent JumpPressed;
}
