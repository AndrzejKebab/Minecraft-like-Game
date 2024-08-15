using Unity.Entities;
using UnityEngine;

namespace PatataGames
{
	public partial class VoxelGameSystemGroup : ComponentSystemGroup { }

	[UpdateInGroup(typeof(VoxelGameSystemGroup))]
	public partial class VoxelGameWorld : SystemBase
	{
		private BeginSimulationEntityCommandBufferSystem beginSimulationECBSystem;
		private EndSimulationEntityCommandBufferSystem endSimulationECBSystem;

		protected override void OnCreate()
		{
			base.OnCreate();
			beginSimulationECBSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
			endSimulationECBSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
		}

		protected override void OnUpdate()
		{
		}
	}

	[UpdateInGroup(typeof(VoxelGameSystemGroup), OrderFirst = true)]
	public partial class BeginVoxelGameCommandBufferSystem : EntityCommandBufferSystem { }

	[UpdateInGroup(typeof(VoxelGameSystemGroup), OrderLast = true)]
	public partial class EndVoxelGameCommandBufferSystem : EntityCommandBufferSystem { }
}
