using System;
using System.Collections.Generic;
using _Project.WorldGeneration.Blocks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace _Project
{
	public class TextureArrayGenerator : MonoBehaviour
	{
		private static readonly int baseMapArray     = Shader.PropertyToID("_BaseMapArray");
		private static readonly int normalMapArray   = Shader.PropertyToID("_NormalMapArray");
		private static readonly int specularMapArray = Shader.PropertyToID("_SpecularMapArray");
		private static readonly int overlayMapArray  = Shader.PropertyToID("_OverlayMapArray");

		[Header("Array Settings")] [SerializeField]
		private int fallbackTextureSize = 16;

		[SerializeField] private bool generateMipMaps = true;

		public class TextureMapping
		{
			public NativeTexturesIDLayer Base;
			public NativeTexturesIDLayer Normal;
			public NativeTexturesIDLayer Specular;
			public NativeTexturesIDLayer Overlay;
		}

		public Dictionary<BlockDataSo, TextureMapping> GenerateTextureArrays(
			BlockDataSo[] allBlocks, Material chunkMaterial)
		{
			var texSize = fallbackTextureSize;
			foreach (BlockDataSo so in allBlocks)
			foreach (BlockTexturesLayer layer in so.TexturesLayer)
			{
				Texture2D first = layer.FrontTexture ?? layer.BackTexture ??
				                  layer.TopTexture ?? layer.BottomTexture ??
				                  layer.RightTexture ?? layer.LeftTexture;
				if (first == null) continue;
				texSize = first.width;
				goto sizeDetected;
			}

			sizeDetected:

			var sentinel = new Texture2D(1, 1) { name = "__slot0_sentinel__" };

			var baseTex     = new List<Texture2D> { sentinel };
			var normalTex   = new List<Texture2D> { sentinel };
			var specularTex = new List<Texture2D> { sentinel };
			var overlayTex  = new List<Texture2D> { sentinel };

			var baseDict     = new Dictionary<Texture2D, ushort> { { sentinel, 0 } };
			var normalDict   = new Dictionary<Texture2D, ushort> { { sentinel, 0 } };
			var specularDict = new Dictionary<Texture2D, ushort> { { sentinel, 0 } };
			var overlayDict  = new Dictionary<Texture2D, ushort> { { sentinel, 0 } };

			var mappings = new Dictionary<BlockDataSo, TextureMapping>();
			foreach (BlockDataSo so in allBlocks)
				mappings[so] = new TextureMapping
				               {
					               Base     = BuildLayer(so, TextureType.Base, baseTex, baseDict),
					               Normal   = BuildLayer(so, TextureType.Normal, normalTex, normalDict),
					               Specular = BuildLayer(so, TextureType.Specular, specularTex, specularDict),
					               Overlay  = BuildLayer(so, TextureType.Overlay, overlayTex, overlayDict)
				               };

			Texture2DArray baseArray =
				BuildArray(baseTex, texSize, GraphicsFormat.RGBA_BC7_SRGB, generateMipMaps, "BlockBase");
			Texture2DArray normalArray = BuildArray(normalTex, texSize, GraphicsFormat.RGBA_BC7_UNorm, generateMipMaps,
			                                        "BlockNormal");
			Texture2DArray specularArray = BuildArray(specularTex, texSize, GraphicsFormat.RGBA_BC7_UNorm,
			                                          generateMipMaps, "BlockSpecular");
			Texture2DArray overlayArray = BuildArray(overlayTex, texSize, GraphicsFormat.RGBA_BC7_SRGB, generateMipMaps,
			                                         "BlockOverlay");

			if (chunkMaterial == null) return mappings;
			chunkMaterial.SetTexture(baseMapArray, baseArray);
			chunkMaterial.SetTexture(normalMapArray, normalArray);
			chunkMaterial.SetTexture(specularMapArray, specularArray);
			chunkMaterial.SetTexture(overlayMapArray, overlayArray);

			return mappings;
		}

		private static NativeTexturesIDLayer BuildLayer(
			BlockDataSo     so,   TextureType                   type,
			List<Texture2D> pool, Dictionary<Texture2D, ushort> dict)
		{
			foreach (BlockTexturesLayer l in so.TexturesLayer)
			{
				if (l.TextureType != type) continue;

				var layer = new NativeTexturesIDLayer(
				                                      Register(l.FrontTexture, pool, dict),
				                                      Register(l.BackTexture, pool, dict),
				                                      Register(l.TopTexture, pool, dict),
				                                      Register(l.BottomTexture, pool, dict),
				                                      Register(l.LeftTexture, pool, dict),
				                                      Register(l.RightTexture, pool, dict));

				switch (type)
				{
					case TextureType.Base:     so.Block.BaseTextures     = layer; break;
					case TextureType.Normal:   so.Block.NormalTextures   = layer; break;
					case TextureType.Specular: so.Block.SpecularTextures = layer; break;
					case TextureType.Overlay:  so.Block.OverlayTextures  = layer; break;
					default:                   throw new ArgumentOutOfRangeException(nameof(type), type, null);
				}

				return layer;
			}

			return default;
		}

		private static ushort Register(
			Texture2D tex, List<Texture2D> pool, Dictionary<Texture2D, ushort> dict)
		{
			if (tex == null) return 0;
			if (dict.TryGetValue(tex, out var idx)) return idx;
			idx = (ushort)pool.Count;
			pool.Add(tex);
			dict[tex] = idx;
			return idx;
		}

		private static Texture2DArray BuildArray(
			List<Texture2D> sources, int    size, GraphicsFormat targetFormat,
			bool            mips,    string name)
		{
			var                  arrayMipCount = mips ? Mathf.FloorToInt(Mathf.Log(size, 2)) + 1 : 1;
			TextureCreationFlags flags         = mips ? TextureCreationFlags.MipChain : TextureCreationFlags.None;

			var array = new Texture2DArray(size, size, sources.Count, targetFormat, flags)
			            {
				            filterMode = FilterMode.Point,
				            wrapMode   = TextureWrapMode.Repeat,
				            name       = name
			            };

			for (var i = 0; i < sources.Count; i++)
			{
				Texture2D src = sources[i];

				if (src == null || src.name == "__slot0_sentinel__")
					continue;

				if (src.width != size || src.height != size)
				{
					Debug.LogWarning($"[TextureArrayGenerator] Skipped '{src.name}': expected {size}×{size}, got {src.width}×{src.height}.");
					continue;
				}

				if (src.graphicsFormat == targetFormat)
				{
					if (!src.isReadable)
					{
						Debug.LogError($"[TextureArrayGenerator] Cannot use CopyPixels on '{src.name}'! " +
						               $"Please go to the Texture Import Settings and enable 'Read/Write'.");
						continue;
					}

					if (mips && src.mipmapCount <= 1)
						Debug.LogWarning($"[TextureArrayGenerator] '{src.name}' has no mipmaps! Please check 'Generate Mip Maps' in its import settings.");

					var safeMips = Mathf.Min(src.mipmapCount, arrayMipCount);
					for (var mip = 0; mip < safeMips; mip++) array.CopyPixels(src, 0, mip, i, mip);
				}
				else
				{
					var expectsSRGB = targetFormat.ToString().Contains("SRGB");

					Debug.LogError($"[TextureArrayGenerator] FORMAT MISMATCH ON '{src.name}'!\n" +
					               $"Expected: {targetFormat}\n" +
					               $"Texture Has: {src.graphicsFormat}\n" +
					               $"FIX THIS BY: {(expectsSRGB ? "CHECKING" : "UNCHECKING")} 'sRGB (Color Texture)' in the texture's Unity Inspector Importer!");
				}
			}

			array.Apply(false, true);
			return array;
		}
	}
}