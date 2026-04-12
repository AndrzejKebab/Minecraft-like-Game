using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FastNoise2.Editor.NoiseAssets
{
	using Authoring.NoiseAsset;
	using Authoring.NoiseGraph;
	using Bindings;

	public static class NoiseAssetBaking
	{
		public static void BakeIntoAsset(this BakedNoiseTextureAsset noiseAsset, string assetPath)
		{
			Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
			if (!Validate(noiseAsset, texture))
			{
				if (texture)
					AssetDatabase.RemoveObjectFromAsset(texture);

				texture = MakeTexture(noiseAsset);

				AssetDatabase.AddObjectToAsset(texture, assetPath);
				AssetDatabase.ImportAsset(assetPath);
			}

			GenerateNoiseTexture(noiseAsset, texture);
			EditorUtility.SetDirty(texture);
		}

		private static void GenerateNoiseTexture(BakedNoiseTextureAsset noiseAsset, Texture texture)
		{
			switch (noiseAsset.textureOutput)
			{
				case NoiseAssetTextureOutput.Texture2D:
					GenerateNoiseTexture2D(noiseAsset, (Texture2D)texture);
					break;
				case NoiseAssetTextureOutput.Texture3D:
					GenerateNoiseTexture3D(noiseAsset, (Texture3D)texture);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(noiseAsset.textureOutput));
			}
		}

		private static unsafe void GenerateNoiseTexture2D(
			BakedNoiseTextureAsset noiseAsset,
			Texture2D texture
		)
		{
			using FastNoise graph = noiseAsset.graph.Instantiate();

			NativeArray<float> buffer = AllocateNative(
				noiseAsset,
				texture,
				out NativeReference<float2> minMax
			);
			using NativeReference<float2> nativeReference = minMax;

			graph.GenUniformGrid2D(
				buffer.GetUnsafePtr(),
				minMax.GetUnsafePtr(),
				noiseAsset.offset.x,
				noiseAsset.offset.y,
				noiseAsset.resolution.x,
				noiseAsset.resolution.y,
				noiseAsset.frequency,
				noiseAsset.frequency,
				noiseAsset.seed
			);

			texture.Apply(false);
		}

		private static NativeArray<float> AllocateNative(
			BakedNoiseTextureAsset noiseAsset,
			Texture3D texture,
			out NativeReference<float2> minMax
		)
		{
			minMax = default;
			try
			{
				NativeArray<float> buffer = texture.GetPixelData<float>(0);
				minMax = new NativeReference<float2>(
					float2(float.PositiveInfinity, float.NegativeInfinity),
					Allocator.Temp
				);
				return buffer;
			}
			catch
			{
				if (minMax.IsCreated)
					minMax.Dispose();
				throw;
			}
		}

		private static NativeArray<float> AllocateNative(
			BakedNoiseTextureAsset noiseAsset,
			Texture2D texture,
			out NativeReference<float2> minMax
		)
		{
			minMax = default;
			try
			{
				NativeArray<float> buffer = texture.GetRawTextureData<float>();
				minMax = new NativeReference<float2>(
					float2(float.PositiveInfinity, float.NegativeInfinity),
					Allocator.Temp
				);
				return buffer;
			}
			catch
			{
				if (minMax.IsCreated)
					minMax.Dispose();
				throw;
			}
		}

		private static unsafe void GenerateNoiseTexture3D(
			BakedNoiseTextureAsset noiseAsset,
			Texture3D texture
		)
		{
			using FastNoise graph = noiseAsset.graph.Instantiate();

			NativeArray<float> buffer = AllocateNative(
				noiseAsset,
				texture,
				out NativeReference<float2> minMax
			);
			using NativeReference<float2> nativeReference = minMax;

			graph.GenUniformGrid3D(
				buffer.GetUnsafePtr(),
				minMax.GetUnsafePtr(),
				noiseAsset.offset.x,
				noiseAsset.offset.y,
				noiseAsset.offset.z,
				noiseAsset.resolution.x,
				noiseAsset.resolution.y,
				noiseAsset.resolution.z,
				noiseAsset.frequency,
				noiseAsset.frequency,
				noiseAsset.frequency,
				noiseAsset.seed
			);

			texture.Apply(false);
		}

		private static bool Validate(BakedNoiseTextureAsset noiseAsset, Texture existingTexture)
		{
			if (existingTexture == null || !existingTexture)
				return false;
			if (existingTexture.dimension != noiseAsset.Dimension())
				return false;
			if (existingTexture.width != noiseAsset.resolution.x)
				return false;
			if (existingTexture.height != noiseAsset.resolution.y)
				return false;
			if (
				existingTexture.dimension == TextureDimension.Tex3D
				&& ((Texture3D)existingTexture).depth != noiseAsset.resolution.z
			)
				return false;

			return true;
		}

		public static TextureDimension Dimension(this BakedNoiseTextureAsset noiseAsset)
		{
			switch (noiseAsset.textureOutput)
			{
				case NoiseAssetTextureOutput.Texture2D:
					return TextureDimension.Tex2D;
				case NoiseAssetTextureOutput.Texture3D:
					return TextureDimension.Tex3D;
				default:
					throw new ArgumentOutOfRangeException(nameof(noiseAsset.textureOutput));
			}
		}

		public static Texture MakeTexture(BakedNoiseTextureAsset noiseAsset)
		{
			switch (noiseAsset.textureOutput)
			{
				case NoiseAssetTextureOutput.Texture2D:
					return new Texture2D(
						noiseAsset.resolution.x,
						noiseAsset.resolution.y,
						GetTextureFormat(noiseAsset),
						false
					)
					{
						name = noiseAsset.name,
					};
				case NoiseAssetTextureOutput.Texture3D:
					return new Texture3D(
						noiseAsset.resolution.x,
						noiseAsset.resolution.y,
						noiseAsset.resolution.z,
						GetTextureFormat(noiseAsset),
						false
					)
					{
						name = noiseAsset.name,
					};
				default:
					throw new ArgumentOutOfRangeException(nameof(noiseAsset.textureOutput));
			}
		}

		public static TextureFormat GetTextureFormat(BakedNoiseTextureAsset noiseAsset)
		{
			switch (noiseAsset.textureFormat)
			{
				// case NoiseAssetTextureFormat.R8:
				// return TextureFormat.R8;
				// case NoiseAssetTextureFormat.R16:
				// return TextureFormat.R16;
				case NoiseAssetTextureFormat.R32:
					return TextureFormat.RFloat;
				default:
					throw new ArgumentOutOfRangeException(nameof(noiseAsset.textureFormat));
			}
		}
	}
}
