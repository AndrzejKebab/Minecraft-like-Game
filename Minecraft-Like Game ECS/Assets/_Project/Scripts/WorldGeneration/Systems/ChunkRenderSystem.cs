using System.Runtime.InteropServices;
using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class ChunkRenderSystem : SystemBase
	{
		private static readonly int chunkData = Shader.PropertyToID("_ChunkData");
		private static readonly int vertices  = Shader.PropertyToID("vertices");

		private GraphicsBuffer chunkDataBuffer;
		private GraphicsBuffer solidArgsBuffer;
		private GraphicsBuffer fluidArgsBuffer;

		private Material              solidMaterial;
		private Material              waterMaterial;
		private RenderParams          solidRenderParams;
		private RenderParams          fluidRenderParams;
		private MaterialPropertyBlock mpb;

		[StructLayout(LayoutKind.Sequential)]
		private struct GPUChunkData
		{
			public float3 Center;          // world-space CORNER (not midpoint)
			public uint   VertexOffset;
			public uint   IndexOffset;
			public uint   SolidIndexCount;
			public uint   FluidIndexCount;
			public uint   Padding;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct IndirectArgs
		{
			public uint IndexCount;
			public uint InstanceCount;
			public uint StartIndex;
			public int  BaseVertex;    // always 0 — indices are global
			public uint StartInstance; // chunk slot for GetIndirectInstanceID
		}

		private const int MAX_CHUNKS = 15000;

		private GPUChunkData[] cpuChunkData;
		private IndirectArgs[] cpuSolidArgs;
		private IndirectArgs[] cpuFluidArgs;

		private int  _logFrameSkip;
		private bool _loggedOnce;

		protected override void OnCreate()
		{
			unsafe
			{
				chunkDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
				                                     MAX_CHUNKS, sizeof(GPUChunkData));
			}

			solidArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, MAX_CHUNKS, 20);
			fluidArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, MAX_CHUNKS, 20);

			cpuChunkData = new GPUChunkData[MAX_CHUNKS];
			cpuSolidArgs = new IndirectArgs[MAX_CHUNKS];
			cpuFluidArgs = new IndirectArgs[MAX_CHUNKS];

			mpb = new MaterialPropertyBlock();
		}

		protected override void OnUpdate()
		{
			if (solidMaterial == null || waterMaterial == null)
			{
				if (SystemAPI.TryGetSingletonEntity<WorldBlockRegistrySingleton>(out Entity reg) &&
				    EntityManager.HasComponent<ChunkMaterialComponent>(reg))
				{
					var mc        = EntityManager.GetComponentData<ChunkMaterialComponent>(reg);
					solidMaterial = mc.SolidMaterial;
					waterMaterial = mc.WaterMaterial;

					solidRenderParams = new RenderParams
					{
						material          = solidMaterial,
						receiveShadows    = true,
						shadowCastingMode = ShadowCastingMode.On,
						layer = LayerMask.NameToLayer("Chunk"),
						
					};
					fluidRenderParams = new RenderParams
					{
						material          = waterMaterial,
						receiveShadows    = true,
						shadowCastingMode = ShadowCastingMode.Off,
						layer             = LayerMask.NameToLayer("Chunk")
					};
				}
				else return;
			}

			var megaBuffer = World.GetExistingSystemManaged<MegaBufferSystem>();
			if (megaBuffer?.VertexBuffer == null) return;

			var chunkCount = 0;
			var solidDraws = 0;
			var fluidDraws = 0;

			foreach ((RefRO<VoxelMeshAllocation> alloc, RefRO<ChunkPositionComponent> pos) in SystemAPI
				         .Query<RefRO<VoxelMeshAllocation>, RefRO<ChunkPositionComponent>>()
				         .WithAll<IsVisible, HasMesh>()
				         .WithNone<IsEmpty>())
			{
				if (!alloc.ValueRO.IsAllocated) continue;
				if (alloc.ValueRO.SolidIndexCount == 0 && alloc.ValueRO.FluidIndexCount == 0) continue;
				if (chunkCount >= MAX_CHUNKS) break;

				uint slot = (uint)chunkCount;

				cpuChunkData[chunkCount] = new GPUChunkData
				{
					Center          = (float3)pos.ValueRO.WorldPosition, // corner — DO NOT add half-chunk
					VertexOffset    = (uint)alloc.ValueRO.VertexOffset,
					IndexOffset     = (uint)alloc.ValueRO.IndexOffset,
					SolidIndexCount = (uint)alloc.ValueRO.SolidIndexCount,
					FluidIndexCount = (uint)alloc.ValueRO.FluidIndexCount,
					Padding         = 0
				};

				cpuSolidArgs[chunkCount] = new IndirectArgs
				{
					IndexCount    = (uint)alloc.ValueRO.SolidIndexCount,
					InstanceCount = alloc.ValueRO.SolidIndexCount > 0 ? 1u : 0u,
					StartIndex    = (uint)alloc.ValueRO.IndexOffset,
					BaseVertex    = 0,           // indices are already global
					StartInstance = slot         // GetIndirectInstanceID reads this
				};
				if (alloc.ValueRO.SolidIndexCount > 0) solidDraws++;

				uint fluidIndexStart = (uint)(alloc.ValueRO.IndexOffset + alloc.ValueRO.SolidIndexCount);
				cpuFluidArgs[chunkCount] = new IndirectArgs
				{
					IndexCount    = (uint)alloc.ValueRO.FluidIndexCount,
					InstanceCount = alloc.ValueRO.FluidIndexCount > 0 ? 1u : 0u,
					StartIndex    = fluidIndexStart,
					BaseVertex    = 0,
					StartInstance = slot
				};
				if (alloc.ValueRO.FluidIndexCount > 0) fluidDraws++;

				chunkCount++;
			}

			if (!_loggedOnce || ++_logFrameSkip >= 300)
			{
				_loggedOnce   = true;
				_logFrameSkip = 0;
				if (chunkCount == 0)
					Debug.LogWarning("[Render] chunkCount=0 — no visible allocated chunks.");
				else
					Debug.Log($"[Render] chunks={chunkCount}  solid={solidDraws}  fluid={fluidDraws}"
					        + $"  | slot0 center={cpuChunkData[0].Center}"
					        + $"  vOff={cpuChunkData[0].VertexOffset}"
					        + $"  solidIdx={cpuChunkData[0].SolidIndexCount}");
			}

			if (chunkCount == 0) return;

			chunkDataBuffer.SetData(cpuChunkData, 0, 0, chunkCount);
			solidArgsBuffer.SetData(cpuSolidArgs, 0, 0, chunkCount);
			fluidArgsBuffer.SetData(cpuFluidArgs, 0, 0, chunkCount);

			mpb.SetBuffer(vertices,  megaBuffer.VertexBuffer);
			mpb.SetBuffer(chunkData, chunkDataBuffer);

			Camera cam = Camera.main;
			if (cam == null) return;

			var bounds = new Bounds();

			solidRenderParams.matProps    = mpb;
			solidRenderParams.worldBounds = bounds;
			Graphics.RenderPrimitivesIndexedIndirect(
				in solidRenderParams, MeshTopology.Triangles,
				megaBuffer.IndexBuffer, solidArgsBuffer, chunkCount);

			fluidRenderParams.matProps    = mpb;
			fluidRenderParams.worldBounds = bounds;
			Graphics.RenderPrimitivesIndexedIndirect(
				in fluidRenderParams, MeshTopology.Triangles,
				megaBuffer.IndexBuffer, fluidArgsBuffer, chunkCount);
		}

		protected override void OnDestroy()
		{
			chunkDataBuffer?.Release();
			solidArgsBuffer?.Release();
			fluidArgsBuffer?.Release();
		}
	}
}