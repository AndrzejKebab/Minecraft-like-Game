using System;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct FirstPersonCharacterComponent : IComponentData
{
	public float                               GroundMaxSpeed;
	public float                               GroundedMovementSharpness;
	public float                               AirAcceleration;
	public float                               AirMaxSpeed;
	public float                               AirDrag;
	public float                               JumpSpeed;
	public float3                              Gravity;
	public bool                                PreventAirAccelerationAgainstUngroundedHits;
	public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

	// ── Creative-style flight (toggled by double-tapping jump) ───────────────
	public bool  IsFlying;            // persistent flight state
	public float FlySpeed;            // horizontal flight speed (blocks/s)
	public float FlyVerticalSpeed;    // ascend/descend speed (blocks/s)
	public float FlySprintMultiplier; // speed multiplier while sprint is held

	public float MinViewAngle;
	public float MaxViewAngle;

	[NonSerialized] public Entity ViewEntity;

	public float      ViewPitchDegrees;
	public quaternion ViewLocalRotation;
}

[Serializable]
public struct FirstPersonCharacterControl : IComponentData
{
	public float3 MoveVector;
	public float2 LookDegreesDelta;
	public bool   Jump;

	// flight controls
	public float VerticalInput; // +1 ascend (jump), -1 descend (crouch) while flying
	public bool  Sprint;        // fly faster while held
	public bool  ToggleFly;     // edge: flip flight state this tick
}

[Serializable]
public struct FirstPersonCharacterView : IComponentData
{
	[NonSerialized] public Entity CharacterEntity;
}