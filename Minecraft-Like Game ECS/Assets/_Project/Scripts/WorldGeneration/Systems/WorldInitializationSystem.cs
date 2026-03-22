using System.Collections.Generic;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.WorldGeneration.Systems
{
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	public partial class WorldInitializationSystem : SystemBase
	{
		protected override void OnUpdate()
		{
			// Wait until the MonoBehaviour is ready in the scene
			if (WorldSettings.Instance == null) return;

			// Only run this setup once
			if (SystemAPI.HasSingleton<WorldSettingsSingleton>())
			{
				Enabled = false;
				return;
			}

			var    settings       = WorldSettings.Instance;
			Entity settingsEntity = EntityManager.CreateEntity();

			// 1. Setup Settings Singleton
			EntityManager.AddComponentData(settingsEntity, new WorldSettingsSingleton
			                                               {
				                                               Seed        = settings.Seed,
				                                               BiomeHeight = settings.MaxTerrainHeight,
				                                               EncodedNodeTree =
					                                               new FixedString512Bytes(settings.EncodedNodeTree)
			                                               });

			// 2. Setup Material Component
			EntityManager.AddComponentObject(settingsEntity, new ChunkMaterialComponent
			                                                 {
				                                                 Material = settings.ChunkMaterial
			                                                 });

			// 3. Setup Block Registry natively
			var maxId = 0;
			foreach (BlockDataSo so in settings.BlockDataSos)
				if (so.Block.ID > maxId)
					maxId = so.Block.ID;

			var blockRegistry = new WorldBlockRegistrySingleton
			                    {
				                    Blocks = new NativeArray<Block>(maxId + 1, Allocator.Persistent)
			                    };

			var uniqueMeshes = new List<MeshDataSO>();
			var meshToId     = new Dictionary<MeshDataSO, ushort>();

			foreach (BlockDataSo so in settings.BlockDataSos)
			{
				if (so.VoxelData == null || meshToId.ContainsKey(so.VoxelData)) continue;
				meshToId[so.VoxelData] = (ushort)uniqueMeshes.Count;
				uniqueMeshes.Add(so.VoxelData);
			}

			var nativeMeshes = new NativeArray<NativeVoxelMeshData>(uniqueMeshes.Count, Allocator.Persistent);
			for (var i = 0; i < uniqueMeshes.Count; i++)
				nativeMeshes[i] = new NativeVoxelMeshData
				                  {
					                  Vertices = new NativeArray<float3>(uniqueMeshes[i].MeshData.Vertices,
					                                                     Allocator.Persistent),
					                  Triangles = new NativeArray<int4>(uniqueMeshes[i].MeshData.Triangles,
					                                                    Allocator.Persistent)
				                  };
			blockRegistry.Meshes = nativeMeshes;

			foreach (BlockDataSo so in settings.BlockDataSos)
			{
				Block b = so.Block;
				b.MeshID = so.VoxelData != null ? meshToId[so.VoxelData] : (ushort)0;

				// Because we aren't using the TextureArrayGenerator, the values for BaseTextures/NormalTextures 
				// in the SO Inspector are exactly what gets copied here.
				blockRegistry.Blocks[b.ID] = b;
			}

			EntityManager.AddComponentData(settingsEntity, blockRegistry);
		}

		protected override void OnDestroy()
		{
			if (!SystemAPI.TryGetSingleton(out WorldBlockRegistrySingleton reg)) return;
			if (reg.Blocks.IsCreated) reg.Blocks.Dispose();
			if (!reg.Meshes.IsCreated) return;
			for (var i = 0; i < reg.Meshes.Length; i++)
			{
				if (reg.Meshes[i].Vertices.IsCreated) reg.Meshes[i].Vertices.Dispose();
				if (reg.Meshes[i].Triangles.IsCreated) reg.Meshes[i].Triangles.Dispose();
			}

			reg.Meshes.Dispose();
		}
	}
}