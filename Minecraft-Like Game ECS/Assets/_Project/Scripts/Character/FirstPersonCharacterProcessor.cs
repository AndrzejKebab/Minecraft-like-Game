using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.CharacterController;

public struct FirstPersonCharacterUpdateContext
{
    // Here, you may add additional global data for your character updates, such as ComponentLookups, Singletons, NativeCollections, etc...
    // The data you add here will be accessible in your character updates and all of your character "callbacks".

    public void OnSystemCreate(ref SystemState state)
    {
        // Get lookups
    }

    public void OnSystemUpdate(ref SystemState state)
    {
        // Update lookups
    }
}

public struct FirstPersonCharacterProcessor : IKinematicCharacterProcessor<FirstPersonCharacterUpdateContext>
{
    public KinematicCharacterDataAccess CharacterDataAccess;
    public RefRW<FirstPersonCharacterComponent> CharacterComponent;
    public RefRW<FirstPersonCharacterControl> CharacterControl;

    public void PhysicsUpdate(ref FirstPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        ref FirstPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
        ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
        ref float3 characterPosition = ref CharacterDataAccess.LocalTransform.ValueRW.Position;

        // First phase of default character update
        KinematicCharacterUtilities.Update_Initialize(
            in this,
            ref context,
            ref baseContext,
            ref characterBody,
            CharacterDataAccess.CharacterHitsBuffer,
            CharacterDataAccess.DeferredImpulsesBuffer,
            CharacterDataAccess.VelocityProjectionHits,
            baseContext.Time.DeltaTime);

        KinematicCharacterUtilities.Update_ParentMovement(
            in this,
            ref context,
            ref baseContext,
            CharacterDataAccess.CharacterEntity,
            ref characterBody,
            CharacterDataAccess.CharacterProperties.ValueRO,
            CharacterDataAccess.PhysicsCollider.ValueRO,
            CharacterDataAccess.LocalTransform.ValueRO,
            ref characterPosition,
            characterBody.WasGroundedBeforeCharacterUpdate);

        KinematicCharacterUtilities.Update_Grounding(
            in this,
            ref context,
            ref baseContext,
            ref characterBody,
            CharacterDataAccess.CharacterEntity,
            CharacterDataAccess.CharacterProperties.ValueRO,
            CharacterDataAccess.PhysicsCollider.ValueRO,
            CharacterDataAccess.LocalTransform.ValueRO,
            CharacterDataAccess.VelocityProjectionHits,
            CharacterDataAccess.CharacterHitsBuffer,
            ref characterPosition);

        // Update desired character velocity after grounding was detected, but before doing additional processing that depends on velocity
        HandleVelocityControl(ref context, ref baseContext);

        // Second phase of default character update
        KinematicCharacterUtilities.Update_PreventGroundingFromFutureSlopeChange(
            in this,
            ref context,
            ref baseContext,
            CharacterDataAccess.CharacterEntity,
            ref characterBody,
            CharacterDataAccess.CharacterProperties.ValueRO,
            CharacterDataAccess.PhysicsCollider.ValueRO,
            in characterComponent.StepAndSlopeHandling);

        KinematicCharacterUtilities.Update_GroundPushing(
            in this,
            ref context,
            ref baseContext,
            ref characterBody,
            CharacterDataAccess.CharacterProperties.ValueRO,
            CharacterDataAccess.LocalTransform.ValueRO,
            CharacterDataAccess.DeferredImpulsesBuffer,
            characterComponent.Gravity);

        KinematicCharacterUtilities.Update_MovementAndDecollisions(
            in this,
            ref context,
            ref baseContext,
            CharacterDataAccess.CharacterEntity,
            ref characterBody,
            CharacterDataAccess.CharacterProperties.ValueRO,
            CharacterDataAccess.PhysicsCollider.ValueRO,
            CharacterDataAccess.LocalTransform.ValueRO,
            CharacterDataAccess.VelocityProjectionHits,
            CharacterDataAccess.CharacterHitsBuffer,
            CharacterDataAccess.DeferredImpulsesBuffer,
            ref characterPosition);

        KinematicCharacterUtilities.Update_MovingPlatformDetection(
            ref baseContext,
            ref characterBody);

        KinematicCharacterUtilities.Update_ParentMomentum(
            ref baseContext,
            ref characterBody,
            CharacterDataAccess.LocalTransform.ValueRO.Position);

        KinematicCharacterUtilities.Update_ProcessStatefulCharacterHits(
            CharacterDataAccess.CharacterHitsBuffer,
            CharacterDataAccess.StatefulHitsBuffer);
    }

    void HandleVelocityControl(ref FirstPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        var deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
        ref FirstPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
        ref FirstPersonCharacterControl characterControl = ref CharacterControl.ValueRW;

        // Rotate move input and velocity to take into account parent rotation
        if (characterBody.ParentEntity != Entity.Null)
        {
            characterControl.MoveVector = math.rotate(characterBody.RotationFromParent, characterControl.MoveVector);
            characterBody.RelativeVelocity = math.rotate(characterBody.RotationFromParent, characterBody.RelativeVelocity);
        }

        if (characterBody.IsGrounded)
        {
            // Move on ground
            float3 targetVelocity = characterControl.MoveVector * characterComponent.GroundMaxSpeed;
            CharacterControlUtilities.StandardGroundMove_Interpolated(ref characterBody.RelativeVelocity, targetVelocity, characterComponent.GroundedMovementSharpness, deltaTime, characterBody.GroundingUp, characterBody.GroundHit.Normal);

            // Jump
            if (characterControl.Jump)
            {
                CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * characterComponent.JumpSpeed, true, characterBody.GroundingUp);
            }
        }
        else
        {
            // Move in air
            float3 airAcceleration = characterControl.MoveVector * characterComponent.AirAcceleration;
            if (math.lengthsq(airAcceleration) > 0f)
            {
                float3 tmpVelocity = characterBody.RelativeVelocity;
                CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration, characterComponent.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

                // Cancel air acceleration from input if we would hit a non-grounded surface (prevents air-climbing slopes at high air accelerations)
                if (characterComponent.PreventAirAccelerationAgainstUngroundedHits
                    && KinematicCharacterUtilities.MovementWouldHitNonGroundedObstruction(
                        in this,
                        ref context,
                        ref baseContext,
                        CharacterDataAccess.CharacterProperties.ValueRO,
                        CharacterDataAccess.LocalTransform.ValueRO,
                        CharacterDataAccess.CharacterEntity,
                        CharacterDataAccess.PhysicsCollider.ValueRO,
                        characterBody.RelativeVelocity * deltaTime,
                        out ColliderCastHit hit))
                {
                    characterBody.RelativeVelocity = tmpVelocity;
                }
            }

            // Gravity
            CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, characterComponent.Gravity, deltaTime);

            // Drag
            CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, characterComponent.AirDrag);
        }
    }

    public void VariableUpdate(ref FirstPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
        ref FirstPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
        ref FirstPersonCharacterControl characterControl = ref CharacterControl.ValueRW;
        ref quaternion characterRotation = ref CharacterDataAccess.LocalTransform.ValueRW.Rotation;

        // Add rotation from parent body to the character rotation
        // (this is for allowing a rotating moving platform to rotate your character as well, and handle interpolation properly)
        KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref characterRotation, characterBody.RotationFromParent, baseContext.Time.DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);

        // Compute character & view rotations from rotation input
        FirstPersonCharacterUtilities.ComputeFinalRotationsFromRotationDelta(
            ref characterRotation,
            ref characterComponent.ViewPitchDegrees,
            characterControl.LookDegreesDelta,
            0f,
            characterComponent.MinViewAngle,
            characterComponent.MaxViewAngle,
            out var canceledPitchDegrees,
            out characterComponent.ViewLocalRotation);
    }

    #region Character Processor Callbacks

    public void UpdateGroundingUp(
        ref FirstPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext)
    {
        ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;

        KinematicCharacterUtilities.Default_UpdateGroundingUp(
            ref characterBody,
            CharacterDataAccess.LocalTransform.ValueRO.Rotation);
    }

    public bool CanCollideWithHit(
        ref FirstPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        in BasicHit hit)
    {
        return PhysicsUtilities.IsCollidable(hit.Material);
    }

    public bool IsGroundedOnHit(
        ref FirstPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        in BasicHit hit,
        int groundingEvaluationType)
    {
        FirstPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;

        return KinematicCharacterUtilities.Default_IsGroundedOnHit(
            in this,
            ref context,
            ref baseContext,
            CharacterDataAccess.CharacterEntity,
            CharacterDataAccess.PhysicsCollider.ValueRO,
            CharacterDataAccess.CharacterBody.ValueRO,
            CharacterDataAccess.CharacterProperties.ValueRO,
            in hit,
            in characterComponent.StepAndSlopeHandling,
            groundingEvaluationType);
    }

    public void OnMovementHit(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float hitDistance)
    {
        ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
        ref float3 characterPosition = ref CharacterDataAccess.LocalTransform.ValueRW.Position;
        FirstPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;

        KinematicCharacterUtilities.Default_OnMovementHit(
            in this,
            ref context,
            ref baseContext,
            ref characterBody,
            CharacterDataAccess.CharacterEntity,
            CharacterDataAccess.CharacterProperties.ValueRO,
            CharacterDataAccess.PhysicsCollider.ValueRO,
            CharacterDataAccess.LocalTransform.ValueRO,
            ref characterPosition,
            CharacterDataAccess.VelocityProjectionHits,
            ref hit,
            ref remainingMovementDirection,
            ref remainingMovementLength,
            originalVelocityDirection,
            hitDistance,
            characterComponent.StepAndSlopeHandling.StepHandling,
            characterComponent.StepAndSlopeHandling.MaxStepHeight,
            characterComponent.StepAndSlopeHandling.CharacterWidthForStepGroundingCheck);
    }

    public void OverrideDynamicHitMasses(
        ref FirstPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        ref PhysicsMass characterMass,
        ref PhysicsMass otherMass,
        BasicHit hit)
    {
        // Custom mass overrides
    }

    public void ProjectVelocityOnHits(
        ref FirstPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        ref float3 velocity,
        ref bool characterIsGrounded,
        ref BasicHit characterGroundHit,
        in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
        float3 originalVelocityDirection)
    {
        FirstPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;

        KinematicCharacterUtilities.Default_ProjectVelocityOnHits(
            ref velocity,
            ref characterIsGrounded,
            ref characterGroundHit,
            in velocityProjectionHits,
            originalVelocityDirection,
            characterComponent.StepAndSlopeHandling.ConstrainVelocityToGroundPlane,
            in CharacterDataAccess.CharacterBody.ValueRO);
    }
    #endregion
}
