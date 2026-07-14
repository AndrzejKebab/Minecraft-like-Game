using _Project.WorldGeneration.Components;
using Unity.Burst;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
public partial class FirstPersonPlayerInputsSystem : SystemBase
{
	// max seconds between two jump taps to count as a double-tap (toggles flight)
	private const double DoubleTapWindow = 0.30;
	private       double m_LastJumpTapTime = double.NegativeInfinity;

	protected override void OnCreate()
	{
		RequireForUpdate<FixedTickSystem.Singleton>();
		RequireForUpdate(SystemAPI.QueryBuilder()
		                          .WithAll<FirstPersonPlayer, FirstPersonPlayerInputs, PlayerInteractionState>()
		                          .Build());
	}

	protected override void OnUpdate()
	{
		var tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;
		var now  = SystemAPI.Time.ElapsedTime;

		// double-tap jump → toggle flight (detected once per frame, not per player)
		var jumpTapped   = Keyboard.current.spaceKey.wasPressedThisFrame;
		var flyToggled   = false;
		if (jumpTapped)
		{
			if (now - m_LastJumpTapTime <= DoubleTapWindow) flyToggled = true;
			m_LastJumpTapTime = now;
		}

		// vertical flight input: jump held ascends, crouch (C / left ctrl) descends
		var ascend  = Keyboard.current.spaceKey.isPressed;
		var descend = Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
		var vertical = (ascend ? 1f : 0f) + (descend ? -1f : 0f);
		var sprint   = Keyboard.current.leftShiftKey.isPressed;

		foreach ((RefRW<FirstPersonPlayerInputs> playerInputs, RefRW<PlayerInteractionState> interactState,
		          RefRO<FirstPersonPlayer> player) in SystemAPI
			         .Query<RefRW<FirstPersonPlayerInputs>, RefRW<PlayerInteractionState>, RefRO<FirstPersonPlayer>>())
		{
			playerInputs.ValueRW.MoveInput = new float2
			                                 {
				                                 x = (Keyboard.current.dKey.isPressed ? 1f : 0f) +
				                                     (Keyboard.current.aKey.isPressed ? -1f : 0f),
				                                 y = (Keyboard.current.wKey.isPressed ? 1f : 0f) +
				                                     (Keyboard.current.sKey.isPressed ? -1f : 0f)
			                                 };

			playerInputs.ValueRW.LookInput     = Mouse.current.delta.ReadValue() * player.ValueRO.LookInputSensitivity;
			playerInputs.ValueRW.VerticalInput = vertical;
			playerInputs.ValueRW.SprintHeld    = sprint;

			if (jumpTapped) playerInputs.ValueRW.JumpPressed.Set(tick);
			if (flyToggled) playerInputs.ValueRW.FlyTogglePressed.Set(tick);

			interactState.ValueRW.BreakPressed = Mouse.current.leftButton.wasPressedThisFrame;
			interactState.ValueRW.PlacePressed = Mouse.current.rightButton.wasPressedThisFrame;
			interactState.ValueRW.ScrollDelta  = Mouse.current.scroll.ReadValue().y;
		}
	}
}

/// <summary>
///     Apply inputs that need to be read at a variable rate
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct FirstPersonPlayerVariableStepControlSystem : ISystem
{
	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerInputs>().Build());
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		foreach ((RefRO<FirstPersonPlayerInputs> playerInputs, RefRO<FirstPersonPlayer> player) in SystemAPI
			         .Query<RefRO<FirstPersonPlayerInputs>, RefRO<FirstPersonPlayer>>().WithAll<Simulate>())
			if (SystemAPI.HasComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter))
			{
				var characterControl =
					SystemAPI.GetComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter);

				characterControl.LookDegreesDelta = playerInputs.ValueRO.LookInput;

				SystemAPI.SetComponent(player.ValueRO.ControlledCharacter, characterControl);
			}
	}
}

/// <summary>
///     Apply inputs that need to be read at a fixed rate.
///     It is necessary to handle this as part of the fixed step group, in case your framerate is lower than the fixed step
///     rate.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct FirstPersonPlayerFixedStepControlSystem : ISystem
{
	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate<FixedTickSystem.Singleton>();
		state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerInputs>().Build());
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		var tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;

		foreach ((RefRO<FirstPersonPlayerInputs> playerInputs, RefRO<FirstPersonPlayer> player) in SystemAPI
			         .Query<RefRO<FirstPersonPlayerInputs>, RefRO<FirstPersonPlayer>>().WithAll<Simulate>())
			if (SystemAPI.HasComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter))
			{
				var characterControl =
					SystemAPI.GetComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter);

				quaternion characterRotation =
					SystemAPI.GetComponent<LocalTransform>(player.ValueRO.ControlledCharacter).Rotation;

				// Move
				float3 characterForward = MathUtilities.GetForwardFromRotation(characterRotation);
				float3 characterRight   = MathUtilities.GetRightFromRotation(characterRotation);
				characterControl.MoveVector = playerInputs.ValueRO.MoveInput.y * characterForward +
				                              playerInputs.ValueRO.MoveInput.x * characterRight;
				characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

				// Jump
				characterControl.Jump = playerInputs.ValueRO.JumpPressed.IsSet(tick);

				// Flight
				characterControl.ToggleFly     = playerInputs.ValueRO.FlyTogglePressed.IsSet(tick);
				characterControl.VerticalInput = playerInputs.ValueRO.VerticalInput;
				characterControl.Sprint        = playerInputs.ValueRO.SprintHeld;

				SystemAPI.SetComponent(player.ValueRO.ControlledCharacter, characterControl);
			}
	}
}