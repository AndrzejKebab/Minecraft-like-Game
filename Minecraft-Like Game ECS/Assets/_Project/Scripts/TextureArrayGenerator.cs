using System.Collections.Generic;
using _Project.WorldGeneration;
using _Project.WorldGeneration.Blocks;
using _Project.WorldGeneration.Components;
using _Project.WorldGeneration.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
	public class TextureArrayGenerator : MonoBehaviour
	{
		private static readonly int baseMapArray     = Shader.PropertyToID("_BaseMapArray");
		private static readonly int normalMapArray   = Shader.PropertyToID("_NormalMapArray");
		private static readonly int specularMapArray = Shader.PropertyToID("_SpecularMapArray");
		private static readonly int overlayMapArray  = Shader.PropertyToID("_OverlayMapArray");

		[Header("Target Material")] 
		[SerializeField] private Material chunkMaterial;

		[Header("Array Settings")]
		[SerializeField] private int fallbackTextureSize = 16;

		[SerializeField] private bool generateMipMaps = true;

		private void Start() => Initialize();

		private void Initialize()
		{
			if (WorldSettings.Instance == null)
			{
				Debug.LogError("[TextureArrayGenerator] WorldSettings.Instance is null.");
				return;
			}

			BlockDataSo[] allBlocks = WorldSettings.Instance.BlockDataSos;
			if (allBlocks == null || allBlocks.Length == 0)
			{
				Debug.LogError("[TextureArrayGenerator] No BlockDataSos found in WorldSettings.");
				return;
			}

			// ---------------------------------------------------------------
			// 1. Detect texture size from the first valid texture found
			// ---------------------------------------------------------------
			var           texSize   = fallbackTextureSize;
			var texFormat = TextureFormat.RGBA32;
			foreach (BlockDataSo so in allBlocks)
			{
				foreach (BlockTexturesLayer layer in so.TexturesLayer)
				{
					Texture2D first = layer.FrontTexture ?? layer.BackTexture ?? layer.TopTexture
					                  ?? layer.BottomTexture ?? layer.RightTexture ?? layer.LeftTexture;
					if (first == null) continue;
					texSize   = first.width;
					texFormat = TextureFormat.ARGB32;
					goto sizeDetected;
				}
			}

			sizeDetected:

			// ---------------------------------------------------------------
			// 2. Build per-channel texture pools
			//    Index 0 is always a blank fallback so unassigned faces are safe
			// ---------------------------------------------------------------
			var baseTex     = new List<Texture2D> { MakeBlank(texSize, texFormat) };
			var normalTex   = new List<Texture2D> { MakeBlankNormal(texSize) };
			var specularTex = new List<Texture2D> { MakeBlank(texSize, texFormat) };
			var overlayTex  = new List<Texture2D> { MakeBlank(texSize, texFormat) }; // overlay base-color

			var baseDict     = new Dictionary<Texture2D, ushort> { { baseTex[0], 0 } };
			var normalDict   = new Dictionary<Texture2D, ushort> { { normalTex[0], 0 } };
			var specularDict = new Dictionary<Texture2D, ushort> { { specularTex[0], 0 } };
			var overlayDict  = new Dictionary<Texture2D, ushort> { { overlayTex[0], 0 } };

			// ---------------------------------------------------------------
			// 3. Mesh deduplication (same as WorldInitializationSystem)
			// ---------------------------------------------------------------
			var maxId = 0;
			foreach (BlockDataSo so in allBlocks)
				if (so.Block.ID > maxId)
					maxId = so.Block.ID;

			var uniqueMeshes = new List<MeshDataSO>();
			var meshToId     = new Dictionary<MeshDataSO, ushort>();
			foreach (BlockDataSo so in allBlocks)
			{
				if (so.VoxelData == null || meshToId.ContainsKey(so.VoxelData)) continue;
				meshToId[so.VoxelData] = (ushort)uniqueMeshes.Count;
				uniqueMeshes.Add(so.VoxelData);
			}

			// ---------------------------------------------------------------
			// 4. Build the native block registry, registering all textures
			// ---------------------------------------------------------------
			var blocks = new NativeArray<Block>(maxId + 1, Allocator.Persistent);

			foreach (BlockDataSo so in allBlocks)
			{
				Block b = so.Block;
				b.MeshID = so.VoxelData != null ? meshToId[so.VoxelData] : (ushort)0;
				b.Name   = new FixedString32Bytes(so.BlockName);

				b.BaseTextures     = BuildLayer(so, TextureType.Base, baseTex, baseDict);
				b.NormalTextures   = BuildLayer(so, TextureType.Normal, normalTex, normalDict);
				b.SpecularTextures = BuildLayer(so, TextureType.Specular, specularTex, specularDict);
				b.OverlayTextures  = BuildLayer(so, TextureType.Overlay, overlayTex, overlayDict);

				blocks[b.ID] = b;
			}

			// ---------------------------------------------------------------
			// 5. Build the three Texture2DArrays and upload to the material
			// ---------------------------------------------------------------
			Texture2DArray baseArray = BuildArray(baseTex, texSize, texFormat, generateMipMaps, "BlockBase");
			Texture2DArray normalArray =
				BuildArray(normalTex, texSize, TextureFormat.RGBA32, generateMipMaps, "BlockNormal");
			Texture2DArray specularArray =
				BuildArray(specularTex, texSize, texFormat, generateMipMaps, "BlockSpecular");
			Texture2DArray overlayArray = BuildArray(overlayTex, texSize, texFormat, generateMipMaps, "BlockOverlay");

			if (chunkMaterial != null)
			{
				chunkMaterial.SetTexture(baseMapArray, baseArray);
				chunkMaterial.SetTexture(normalMapArray, normalArray);
				chunkMaterial.SetTexture(specularMapArray, specularArray);
				chunkMaterial.SetTexture(overlayMapArray, overlayArray);
			}

			// ---------------------------------------------------------------
			// 6. Push native meshes + registry into ECS world
			// ---------------------------------------------------------------
			EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

			// Clean up any prior registry
			EntityQuery oldQuery = em.CreateEntityQuery(ComponentType.ReadWrite<WorldBlockRegistrySingleton>());
			if (!oldQuery.IsEmpty)
			{
				var old = oldQuery.GetSingleton<WorldBlockRegistrySingleton>();
				if (old.Blocks.IsCreated) old.Blocks.Dispose();
				if (old.Meshes.IsCreated)
				{
					for (var i = 0; i < old.Meshes.Length; i++)
					{
						if (old.Meshes[i].Vertices.IsCreated) old.Meshes[i].Vertices.Dispose();
						if (old.Meshes[i].Triangles.IsCreated) old.Meshes[i].Triangles.Dispose();
					}

					old.Meshes.Dispose();
				}

				em.DestroyEntity(oldQuery.GetSingletonEntity());
			}

			oldQuery.Dispose();

			var nativeMeshes = new NativeArray<NativeVoxelMeshData>(uniqueMeshes.Count, Allocator.Persistent);
			for (var i = 0; i < uniqueMeshes.Count; i++)
				nativeMeshes[i] = new NativeVoxelMeshData
				                  {
					                  Vertices = new NativeArray<float3>(uniqueMeshes[i].MeshData.Vertices,
					                                                     Allocator.Persistent),
					                  Triangles = new NativeArray<int4>(uniqueMeshes[i].MeshData.Triangles,
					                                                    Allocator.Persistent)
				                  };

			Entity regEntity = em.CreateEntity();
			em.AddComponentData(regEntity, new WorldBlockRegistrySingleton
			                               {
				                               Blocks = blocks,
				                               Meshes = nativeMeshes
			                               });

			// If WorldInitializationSystem already ran as a fallback, disable it so it doesn't
			// try to create a second registry singleton on the next frame
			var initSys = World.DefaultGameObjectInjectionWorld
			                   .GetExistingSystemManaged<WorldInitializationSystem>();
			if (initSys != null) initSys.Enabled = false;

			Debug.Log($"[TextureArrayGenerator] Done. " +
			          $"Base:{baseTex.Count} Normal:{normalTex.Count} " +
			          $"Specular:{specularTex.Count} Overlay:{overlayTex.Count} textures. " +
			          $"{uniqueMeshes.Count} meshes. {maxId + 1} block slots.");
		}

		// -------------------------------------------------------------------
		// Helpers
		// -------------------------------------------------------------------

		private static NativeTexturesIDLayer BuildLayer(
			BlockDataSo     so,   TextureType                   type,
			List<Texture2D> pool, Dictionary<Texture2D, ushort> dict)
		{
			foreach (BlockTexturesLayer l in so.TexturesLayer)
			{
				if (l.TextureType != type) continue;
				return new NativeTexturesIDLayer(
				                                 front: Register(l.FrontTexture, pool, dict),
				                                 back: Register(l.BackTexture, pool, dict),
				                                 top: Register(l.TopTexture, pool, dict),
				                                 bottom: Register(l.BottomTexture, pool, dict),
				                                 left: Register(l.LeftTexture, pool, dict),
				                                 right: Register(l.RightTexture, pool, dict));
			}

			return default; // all zeros → fallback slot
		}

		private static ushort Register(Texture2D tex, List<Texture2D> pool, Dictionary<Texture2D, ushort> dict)
		{
			if (tex == null) return 0;
			if (dict.TryGetValue(tex, out var idx)) return idx;
			idx = (ushort)pool.Count;
			pool.Add(tex);
			dict[tex] = idx;
			return idx;
		}

		private static Texture2DArray BuildArray(
			List<Texture2D> sources, int size, TextureFormat format, bool mips, string name)
		{
			var array = new Texture2DArray(size, size, sources.Count, format, mips, false)
			            {
				            filterMode = FilterMode.Point,
				            wrapMode   = TextureWrapMode.Clamp,
				            name       = name
			            };

			for (var i = 0; i < sources.Count; i++)
			{
				Texture2D src = sources[i];
				if (src == null) continue;

				// Size/format mismatch: blit into a correctly-sized temporary
				if (src.width != size || src.height != size || src.format != format)
				{
					var tmp = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
					Graphics.Blit(src, tmp);
					var read = new Texture2D(size, size, format, mips);
					RenderTexture.active = tmp;
					read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
					read.Apply(mips);
					RenderTexture.active = null;
					tmp.Release();
					src = read;
				}

				var mipCount = mips ? src.mipmapCount : 1;
				for (var mip = 0; mip < mipCount; mip++)
					Graphics.CopyTexture(src, 0, mip, array, i, mip);
			}

			array.Apply(false, true);
			return array;
		}

		private static Texture2D MakeBlank(int size, TextureFormat format)
		{
			var       t                                       = new Texture2D(size, size, format, false);
			var pixels                                  = new Color32[size * size];
			for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
			t.SetPixels32(pixels);
			t.Apply();
			return t;
		}

		private static Texture2D MakeBlankNormal(int size)
		{
			// Flat normal (0.5, 0.5, 1.0, 1.0) in tangent space
			var       t                                       = new Texture2D(size, size, TextureFormat.RGBA32, false);
			var pixels                                  = new Color32[size * size];
			for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(128, 128, 255, 255);
			t.SetPixels32(pixels);
			t.Apply();
			return t;
		}
	}
}