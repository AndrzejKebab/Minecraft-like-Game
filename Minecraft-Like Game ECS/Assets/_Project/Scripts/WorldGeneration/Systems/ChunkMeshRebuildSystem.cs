using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateBefore(typeof(ChunkMeshBuilderSystem))]
	public partial class ChunkRebuildSystem : SystemBase
	{
		protected override void OnUpdate()
		{
			var ecb = new EntityCommandBuffer(Allocator.TempJob);

			foreach (var (meshData, entity) in SystemAPI.Query<ChunkMeshData>().WithAll<NeedsMeshRebuild>().WithEntityAccess())
			{
				// 1. Dispose & Wipe old visual Mesh
				meshData.Dispose();
				ecb.RemoveComponent<ChunkMeshData>(entity);

				if (SystemAPI.HasComponent<HasRenderMesh>(entity))
				{
					ecb.RemoveComponent<HasRenderMesh>(entity);
				}

				// 2. Dispose & Wipe old Physics Collider
				if (SystemAPI.HasComponent<PhysicsCollider>(entity))
				{
					var phys = SystemAPI.GetComponent<PhysicsCollider>(entity);
					if (phys.Value.IsCreated) phys.Value.Dispose();
					ecb.RemoveComponent<PhysicsCollider>(entity);
				}

				if (SystemAPI.HasComponent<HasCollider>(entity))
				{
					ecb.RemoveComponent<HasCollider>(entity);
				}

				// 3. Mark for queue cycle to trigger Builder job
				ecb.RemoveComponent<NeedsMeshRebuild>(entity);
				ecb.AddComponent<NeedsRender>(entity); 
			}

			ecb.Playback(EntityManager);
			ecb.Dispose();
		}
	}
}