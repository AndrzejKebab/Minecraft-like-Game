using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct FirstPersonCharacterPhysicsUpdateSystem : ISystem
{
	private EntityQuery                       m_CharacterQuery;
	private FirstPersonCharacterUpdateContext m_Context;
	private KinematicCharacterUpdateContext   m_BaseContext;

	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		m_CharacterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
		                                              .WithAll<FirstPersonCharacterComponent,
			                                              FirstPersonCharacterControl>()
		                                              .Build(ref state);

		m_Context = new FirstPersonCharacterUpdateContext();
		m_Context.OnSystemCreate(ref state);
		m_BaseContext = new KinematicCharacterUpdateContext();
		m_BaseContext.OnSystemCreate(ref state);

		state.RequireForUpdate(m_CharacterQuery);
		state.RequireForUpdate<PhysicsWorldSingleton>();
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		m_Context.OnSystemUpdate(ref state);
		m_BaseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

		var job = new FirstPersonCharacterPhysicsUpdateJob
		          {
			          Context     = m_Context,
			          BaseContext = m_BaseContext
		          };
		job.ScheduleParallel();
	}

	[BurstCompile]
	[WithAll(typeof(Simulate))]
	public partial struct FirstPersonCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
	{
		public FirstPersonCharacterUpdateContext Context;
		public KinematicCharacterUpdateContext   BaseContext;

		public void Execute(
			Entity                                           entity,
			RefRW<LocalTransform>                            localTransform,
			RefRW<KinematicCharacterProperties>              characterProperties,
			RefRW<KinematicCharacterBody>                    characterBody,
			RefRW<PhysicsCollider>                           physicsCollider,
			RefRW<FirstPersonCharacterComponent>             characterComponent,
			RefRW<FirstPersonCharacterControl>               characterControl,
			DynamicBuffer<KinematicCharacterHit>             characterHitsBuffer,
			DynamicBuffer<StatefulKinematicCharacterHit>     statefulHitsBuffer,
			DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
			DynamicBuffer<KinematicVelocityProjectionHit>    velocityProjectionHits)
		{
			var characterProcessor = new FirstPersonCharacterProcessor
			                         {
				                         CharacterDataAccess = new KinematicCharacterDataAccess(
					                          entity,
					                          localTransform,
					                          characterProperties,
					                          characterBody,
					                          physicsCollider,
					                          characterHitsBuffer,
					                          statefulHitsBuffer,
					                          deferredImpulsesBuffer,
					                          velocityProjectionHits
					                         ),
				                         CharacterComponent = characterComponent,
				                         CharacterControl   = characterControl
			                         };

			characterProcessor.PhysicsUpdate(ref Context, ref BaseContext);
		}

		public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
		                         in v128           chunkEnabledMask)
		{
			BaseContext.EnsureCreationOfTmpCollections();
			return true;
		}

		public void OnChunkEnd(in ArchetypeChunk chunk,            int  unfilteredChunkIndex, bool useEnabledMask,
		                       in v128           chunkEnabledMask, bool chunkWasExecuted)
		{
		}
	}
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(FirstPersonPlayerVariableStepControlSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct FirstPersonCharacterVariableUpdateSystem : ISystem
{
	private EntityQuery                       m_CharacterQuery;
	private FirstPersonCharacterUpdateContext m_Context;
	private KinematicCharacterUpdateContext   m_BaseContext;

	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate<PhysicsWorldSingleton>();
		m_CharacterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
		                                              .WithAll<FirstPersonCharacterComponent,
			                                              FirstPersonCharacterControl>()
		                                              .Build(ref state);

		m_Context = new FirstPersonCharacterUpdateContext();
		m_Context.OnSystemCreate(ref state);
		m_BaseContext = new KinematicCharacterUpdateContext();
		m_BaseContext.OnSystemCreate(ref state);

		state.RequireForUpdate(m_CharacterQuery);
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		m_Context.OnSystemUpdate(ref state);
		m_BaseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

		var variableUpdateJob = new FirstPersonCharacterVariableUpdateJob
		                        {
			                        Context     = m_Context,
			                        BaseContext = m_BaseContext
		                        };
		variableUpdateJob.ScheduleParallel();

		var viewJob = new FirstPersonCharacterViewJob
		              {
			              FirstPersonCharacterLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterComponent>(true)
		              };
		viewJob.ScheduleParallel();
	}

	[BurstCompile]
	[WithAll(typeof(Simulate))]
	public partial struct FirstPersonCharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
	{
		public FirstPersonCharacterUpdateContext Context;
		public KinematicCharacterUpdateContext   BaseContext;

		public void Execute(
			Entity                                           entity,
			RefRW<LocalTransform>                            localTransform,
			RefRW<KinematicCharacterProperties>              characterProperties,
			RefRW<KinematicCharacterBody>                    characterBody,
			RefRW<PhysicsCollider>                           physicsCollider,
			RefRW<FirstPersonCharacterComponent>             characterComponent,
			RefRW<FirstPersonCharacterControl>               characterControl,
			DynamicBuffer<KinematicCharacterHit>             characterHitsBuffer,
			DynamicBuffer<StatefulKinematicCharacterHit>     statefulHitsBuffer,
			DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
			DynamicBuffer<KinematicVelocityProjectionHit>    velocityProjectionHits)
		{
			var characterProcessor = new FirstPersonCharacterProcessor
			                         {
				                         CharacterDataAccess = new KinematicCharacterDataAccess(
					                          entity,
					                          localTransform,
					                          characterProperties,
					                          characterBody,
					                          physicsCollider,
					                          characterHitsBuffer,
					                          statefulHitsBuffer,
					                          deferredImpulsesBuffer,
					                          velocityProjectionHits
					                         ),
				                         CharacterComponent = characterComponent,
				                         CharacterControl   = characterControl
			                         };

			characterProcessor.VariableUpdate(ref Context, ref BaseContext);
		}

		public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
		                         in v128           chunkEnabledMask)
		{
			BaseContext.EnsureCreationOfTmpCollections();
			return true;
		}

		public void OnChunkEnd(in ArchetypeChunk chunk,            int  unfilteredChunkIndex, bool useEnabledMask,
		                       in v128           chunkEnabledMask, bool chunkWasExecuted)
		{
		}
	}

	[BurstCompile]
	[WithAll(typeof(Simulate))]
	public partial struct FirstPersonCharacterViewJob : IJobEntity
	{
		[ReadOnly] public ComponentLookup<FirstPersonCharacterComponent> FirstPersonCharacterLookup;

		private void Execute(ref LocalTransform localTransform, in FirstPersonCharacterView characterView)
		{
			if (FirstPersonCharacterLookup.TryGetComponent(characterView.CharacterEntity,
			                                               out FirstPersonCharacterComponent character))
				localTransform.Rotation = character.ViewLocalRotation;
		}
	}
}