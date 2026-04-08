using System;
using System.Collections.Generic;
using _Project.WorldGeneration.Blocks;
using UnityEngine;

namespace _Project
{
    public class TextureArrayGenerator : MonoBehaviour
    {
        private static readonly int baseMapArray     = Shader.PropertyToID("_BaseMapArray");
        private static readonly int normalMapArray   = Shader.PropertyToID("_NormalMapArray");
        private static readonly int specularMapArray = Shader.PropertyToID("_SpecularMapArray");
        private static readonly int overlayMapArray  = Shader.PropertyToID("_OverlayMapArray");

        [Header("Array Settings")]
        [SerializeField] private int fallbackTextureSize = 16;
        [SerializeField] private bool generateMipMaps = true;

        public class TextureMapping
        {
            public NativeTexturesIDLayer Base;
            public NativeTexturesIDLayer Normal;
            public NativeTexturesIDLayer Specular;
            public NativeTexturesIDLayer Overlay;
        }

        public Dictionary<BlockDataSo, TextureMapping> GenerateTextureArrays(BlockDataSo[] allBlocks, Material chunkMaterial)
        {
            var                 texSize   = fallbackTextureSize;
            const TextureFormat texFormat = TextureFormat.RGBA32;

            foreach (BlockDataSo so in allBlocks)
            {
                foreach (BlockTexturesLayer layer in so.TexturesLayer)
                {
                    Texture2D first = layer.FrontTexture ?? layer.BackTexture ?? layer.TopTexture
                                      ?? layer.BottomTexture ?? layer.RightTexture ?? layer.LeftTexture;
                    if (first == null) continue;
                    texSize = first.width;
                    goto sizeDetected;
                }
            }

            sizeDetected:

            var baseTex     = new List<Texture2D> { MakeBlank(texSize, texFormat) };
            var normalTex   = new List<Texture2D> { MakeBlankNormal(texSize) };
            var specularTex = new List<Texture2D> { MakeBlank(texSize, texFormat) };
            var overlayTex  = new List<Texture2D> { MakeBlank(texSize, texFormat) };

            var baseDict     = new Dictionary<Texture2D, ushort> { { baseTex[0], 0 } };
            var normalDict   = new Dictionary<Texture2D, ushort> { { normalTex[0], 0 } };
            var specularDict = new Dictionary<Texture2D, ushort> { { specularTex[0], 0 } };
            var overlayDict  = new Dictionary<Texture2D, ushort> { { overlayTex[0], 0 } };

            var mappings = new Dictionary<BlockDataSo, TextureMapping>();

            foreach (BlockDataSo so in allBlocks)
            {
                mappings[so] = new TextureMapping()
                               {
                    Base     = BuildLayer(so, TextureType.Base, baseTex, baseDict),
                    Normal   = BuildLayer(so, TextureType.Normal, normalTex, normalDict),
                    Specular = BuildLayer(so, TextureType.Specular, specularTex, specularDict),
                    Overlay  = BuildLayer(so, TextureType.Overlay, overlayTex, overlayDict)
                };
            }

            Texture2DArray baseArray = BuildArray(baseTex, texSize, texFormat, generateMipMaps, "BlockBase");
            Texture2DArray normalArray = BuildArray(normalTex, texSize, TextureFormat.RGBA32, generateMipMaps, "BlockNormal");
            Texture2DArray specularArray = BuildArray(specularTex, texSize, texFormat, generateMipMaps, "BlockSpecular");
            Texture2DArray overlayArray = BuildArray(overlayTex, texSize, texFormat, generateMipMaps, "BlockOverlay");

            if (chunkMaterial == null) return mappings;
            chunkMaterial.SetTexture(baseMapArray, baseArray);
            chunkMaterial.SetTexture(normalMapArray, normalArray);
            chunkMaterial.SetTexture(specularMapArray, specularArray);
            chunkMaterial.SetTexture(overlayMapArray, overlayArray);

            return mappings;
        }

        private static NativeTexturesIDLayer BuildLayer(
            BlockDataSo so, TextureType type,
            List<Texture2D> pool, Dictionary<Texture2D, ushort> dict)
        {
            foreach (BlockTexturesLayer l in so.TexturesLayer)
            {
                if (l.TextureType != type) continue;
                switch (l.TextureType)
                {
                    case TextureType.Base:
                        so.Block.BaseTextures = new NativeTexturesIDLayer(
                                              front: Register(l.FrontTexture, pool, dict),
                                              back: Register(l.BackTexture, pool, dict),
                                              top: Register(l.TopTexture, pool, dict),
                                              bottom: Register(l.BottomTexture, pool, dict),
                                              left: Register(l.LeftTexture, pool, dict),
                                              right: Register(l.RightTexture, pool, dict));
                        return so.Block.BaseTextures;
                    case TextureType.Normal:
                        so.Block.NormalTextures = new NativeTexturesIDLayer(
                                                                           front: Register(l.FrontTexture, pool, dict),
                                                                           back: Register(l.BackTexture, pool, dict),
                                                                           top: Register(l.TopTexture, pool, dict),
                                                                           bottom: Register(l.BottomTexture, pool, dict),
                                                                           left: Register(l.LeftTexture, pool, dict),
                                                                           right: Register(l.RightTexture, pool, dict));
                        return so.Block.NormalTextures;
                    case TextureType.Specular:
                        so.Block.SpecularTextures = new NativeTexturesIDLayer(
                                                                              front: Register(l.FrontTexture, pool, dict),
                                                                              back: Register(l.BackTexture, pool, dict),
                                                                              top: Register(l.TopTexture, pool, dict),
                                                                              bottom: Register(l.BottomTexture, pool, dict),
                                                                              left: Register(l.LeftTexture, pool, dict),
                                                                              right: Register(l.RightTexture, pool, dict));
                        return so.Block.SpecularTextures;
                    case TextureType.Overlay:
                        so.Block.OverlayTextures = new NativeTexturesIDLayer(
                                                                              front: Register(l.FrontTexture, pool, dict),
                                                                              back: Register(l.BackTexture, pool, dict),
                                                                              top: Register(l.TopTexture, pool, dict),
                                                                              bottom: Register(l.BottomTexture, pool, dict),
                                                                              left: Register(l.LeftTexture, pool, dict),
                                                                              right: Register(l.RightTexture, pool, dict));
                        return so.Block.OverlayTextures;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return default; 
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

        private static Texture2DArray BuildArray(List<Texture2D> sources, int size, TextureFormat format, bool mips, string name)
        {
            var array = new Texture2DArray(size, size, sources.Count, format, mips, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name
            };

            for (var i = 0; i < sources.Count; i++)
            {
                Texture2D src = sources[i];
                if (src == null) continue;

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
            var t = new Texture2D(size, size, format, false);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        private static Texture2D MakeBlankNormal(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(128, 128, 255, 255);
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }
    }
}