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
			
			System.Array.Sort(allBlocks, (a, b) => a.Block.ID.CompareTo(b.Block.ID));

			if (allBlocks.Length == 0)
			{
				Debug.LogError("[GameBootstrap] No BlockDataSo found!");
				return;
			}

			Material chunkMaterial = GameDatabase.Instance.ChunkMaterial;

			Dictionary<BlockDataSo, TextureArrayGenerator.TextureMapping> textureMappings = textureGenerator.GenerateTextureArrays(allBlocks, chunkMaterial);

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
				if (so.Block.ID > maxId) maxId = so.Block.ID;

			var nativeBlocks = new NativeArray<Block>(maxId + 1, Allocator.Persistent);
			foreach (BlockDataSo so in allBlocks)
			{
				Block b = so.Block;
				b.MeshID = so.VoxelData != null ? meshToId[so.VoxelData] : (ushort)0;
				b.Name   = new FixedString32Bytes(so.BlockName);
				b.TintColor = so.TintColor;

				if (textureMappings.TryGetValue(so, out TextureArrayGenerator.TextureMapping mapping))
				{
					b.BaseTextures     = mapping.Base;
					b.NormalTextures   = mapping.Normal;
					b.SpecularTextures = mapping.Specular;
					b.OverlayTextures  = mapping.Overlay;
				}

				nativeBlocks[b.ID] = b;
			}

			var nativeMeshes = new NativeArray<NativeVoxelMeshData>(uniqueMeshes.Count, Allocator.Persistent);
			for (var i = 0; i < uniqueMeshes.Count; i++)
			{
				nativeMeshes[i] = new NativeVoxelMeshData
				                  {
					                  Vertices = new NativeArray<float3>(uniqueMeshes[i].MeshData.Vertices, Allocator.Persistent),
					                  Triangles = new NativeArray<int4>(uniqueMeshes[i].MeshData.Triangles, Allocator.Persistent)
				                  };
			}

			EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

			Entity regEntity = em.CreateEntity();
			em.AddComponentData(regEntity, new WorldBlockRegistrySingleton
			                               {
				                               Blocks = nativeBlocks,
				                               Meshes = nativeMeshes
			                               });

			em.AddComponentObject(regEntity, new ChunkMaterialComponent
			                                 {
				                                 Material = chunkMaterial
			                                 });

			Debug.Log($"[GameBootstrap] Game Data Ready! Blocks: {allBlocks.Length}, Meshes: {uniqueMeshes.Count}");
		}
	}
}