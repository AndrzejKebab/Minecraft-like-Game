using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.CharacterController;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
public partial class FirstPersonPlayerInputsSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<FixedTickSystem.Singleton>();
        RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerInputs>().Build());
    }

    protected override void OnUpdate()
    {
        var tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;

#if ENABLE_INPUT_SYSTEM
        foreach ((RefRW<FirstPersonPlayerInputs> playerInputs, RefRO<FirstPersonPlayer> player) in SystemAPI.Query<RefRW<FirstPersonPlayerInputs>, RefRO<FirstPersonPlayer>>())
        {
            playerInputs.ValueRW.MoveInput = new float2
            {
                x = (Keyboard.current.dKey.isPressed ? 1f : 0f) + (Keyboard.current.aKey.isPressed ? -1f : 0f),
                y = (Keyboard.current.wKey.isPressed ? 1f : 0f) + (Keyboard.current.sKey.isPressed ? -1f : 0f),
            };

            playerInputs.ValueRW.LookInput = Mouse.current.delta.ReadValue() * player.ValueRO.LookInputSensitivity;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                playerInputs.ValueRW.JumpPressed.Set(tick);
            }
        }
#endif
    }
}

/// <summary>
/// Apply inputs that need to be read at a variable rate
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
        foreach ((RefRO<FirstPersonPlayerInputs> playerInputs, RefRO<FirstPersonPlayer> player) in SystemAPI.Query<RefRO<FirstPersonPlayerInputs>, RefRO<FirstPersonPlayer>>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter))
            {
                var characterControl = SystemAPI.GetComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter);

                characterControl.LookDegreesDelta = playerInputs.ValueRO.LookInput;

                SystemAPI.SetComponent(player.ValueRO.ControlledCharacter, characterControl);
            }
        }
    }
}

/// <summary>
/// Apply inputs that need to be read at a fixed rate.
/// It is necessary to handle this as part of the fixed step group, in case your framerate is lower than the fixed step rate.
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

        foreach ((RefRO<FirstPersonPlayerInputs> playerInputs, RefRO<FirstPersonPlayer> player) in SystemAPI.Query<RefRO<FirstPersonPlayerInputs>, RefRO<FirstPersonPlayer>>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter))
            {
                var characterControl = SystemAPI.GetComponent<FirstPersonCharacterControl>(player.ValueRO.ControlledCharacter);

                quaternion characterRotation = SystemAPI.GetComponent<LocalTransform>(player.ValueRO.ControlledCharacter).Rotation;

                // Move
                float3 characterForward = MathUtilities.GetForwardFromRotation(characterRotation);
                float3 characterRight = MathUtilities.GetRightFromRotation(characterRotation);
                characterControl.MoveVector = (playerInputs.ValueRO.MoveInput.y * characterForward) + (playerInputs.ValueRO.MoveInput.x * characterRight);
                characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

                // Jump
                characterControl.Jump = playerInputs.ValueRO.JumpPressed.IsSet(tick);

                SystemAPI.SetComponent(player.ValueRO.ControlledCharacter, characterControl);
            }
        }
    }
}
