using System;
using System.Collections.Generic;
using _Project.WorldGeneration;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	public class GameBootstrap : MonoBehaviour
	{
		[SerializeField] private TextureArrayGenerator textureGenerator;

		private void Start()
		{
			InitializeGame();
		}

		private void InitializeGame()
		{
			if (GameDatabase.Instance == null)
			{
				Debug.LogError("[GameBootstrap] GameDatabase is missing!");
				return;
			}

			BlockDataSo[] allBlocks = Resources.LoadAll<BlockDataSo>("Blocks");
			GameDatabase.Instance.AllBlocks = allBlocks;

			Array.Sort(allBlocks, (a, b) => a.Block.ID.CompareTo(b.Block.ID));

			if (allBlocks.Length == 0)
			{
				Debug.LogError("[GameBootstrap] No BlockDataSo found!");
				return;
			}

			Material chunkMaterial = GameDatabase.Instance.ChunkMaterial;
			Material waterMaterial = GameDatabase.Instance.WaterMaterial;

			Debug.Log("[GameBootstrap] Found all blocks. Generating texture arrays...");
			Dictionary<BlockDataSo, TextureArrayGenerator.TextureMapping> textureMappings =
				textureGenerator.GenerateTextureArrays(allBlocks, chunkMaterial);
			Debug.Log("[GameBootstrap] Texture arrays generated. Preparing ECS data...");
			var uniqueMeshes = new List<MeshDataSO>();
			var meshToId     = new Dictionary<MeshDataSO, ushort>();

			foreach (BlockDataSo so in allBlocks)
			{
				if (so.VoxelData == null || meshToId.ContainsKey(so.VoxelData)) continue;
				meshToId[so.VoxelData] = (ushort)uniqueMeshes.Count;
				uniqueMeshes.Add(so.VoxelData);
			}

			var maxId = 0;
			foreach (BlockDataSo so in allBlocks)
				if (so.Block.ID > maxId)
					maxId = so.Block.ID;

			var nativeBlocks = new NativeArray<Block>(maxId + 1, Allocator.Persistent);
			var nativeNames  = new NativeArray<FixedString32Bytes>(maxId + 1, Allocator.Persistent);

			foreach (BlockDataSo so in allBlocks)
			{
				Block b = so.Block;
				b.MeshID    = so.VoxelData != null ? meshToId[so.VoxelData] : (ushort)0;
				b.TintColor = so.TintColor;

				if (textureMappings.TryGetValue(so, out TextureArrayGenerator.TextureMapping mapping))
				{
					b.BaseTextures     = mapping.Base;
					b.NormalTextures   = mapping.Normal;
					b.SpecularTextures = mapping.Specular;
					b.OverlayTextures  = mapping.Overlay;
				}

				nativeBlocks[b.ID] = b;
				nativeNames[b.ID]  = new FixedString32Bytes(so.BlockName);
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

			EntityManager em        = World.DefaultGameObjectInjectionWorld.EntityManager;
			Entity        regEntity = em.CreateEntity();
			var blockRegistrySingleton = new WorldBlockRegistrySingleton
			                             {
				                             Blocks         = nativeBlocks,
				                             BlockNames     = nativeNames,
				                             Meshes         = nativeMeshes,
				                             TreeDensity    = 0.015f,
				                             MinTrunkHeight = 4,
				                             MaxTrunkHeight = 12
			                             };

			blockRegistrySingleton.OreTypes = CreateDefaultOres(blockRegistrySingleton);
			em.AddComponentData(regEntity, blockRegistrySingleton);

			em.AddComponentObject(regEntity, new ChunkMaterialComponent
			                                 {
				                                 SolidMaterial = chunkMaterial,
				                                 WaterMaterial = waterMaterial
			                                 });

			Debug.Log($"[GameBootstrap] Game Data Ready! Blocks: {allBlocks.Length}, Meshes: {uniqueMeshes.Count}");
		}

		private static NativeArray<OreSettings> CreateDefaultOres(WorldBlockRegistrySingleton reg)
		{
			var stone = reg.Blocks[1].ID;
			var ores  = new NativeArray<OreSettings>(3, Allocator.Persistent);

			ores[0] = new OreSettings // Coal  — common, wide Y range
			          {
				          BlockID       = reg.Blocks[13].ID,
				          TargetBlockID = stone,
				          MinWorldY     = -64, MaxWorldY  = 128,
				          VeinsPerChunk = 20, MaxVeinSize = 17, VeinRadius = 2f
			          };
			ores[1] = new OreSettings // Iron  — medium rarity
			          {
				          BlockID       = reg.Blocks[11].ID,
				          TargetBlockID = stone,
				          MinWorldY     = -64, MaxWorldY = 64,
				          VeinsPerChunk = 9, MaxVeinSize = 9, VeinRadius = 1.5f
			          };
			ores[2] = new OreSettings // Diamond — rare, deep only
			          {
				          BlockID       = reg.Blocks[12].ID,
				          TargetBlockID = stone,
				          MinWorldY     = -64, MaxWorldY = 16,
				          VeinsPerChunk = 2, MaxVeinSize = 8, VeinRadius = 1f
			          };
			return ores;
		}
	}
}