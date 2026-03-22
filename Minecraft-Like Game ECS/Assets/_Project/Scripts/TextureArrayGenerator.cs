/*
using System.Collections.Generic;
using _Project.WorldGeneration;
using _Project.WorldGeneration.Blocks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project
{
    public class TextureArrayGenerator : MonoBehaviour
    {
        [SerializeField] private Material material;

        public void Start() => Initialize();

        public void Initialize()
        {
            var allBlocks = Resources.LoadAll<BlockDataSo>("");
            Debug.Log($"Loaded {allBlocks.Length} blocks from Resources!");
            if (allBlocks.Length == 0) return;

            int detectedSize = 16; // Adjust logic as needed; defaulting to 16 for safety.
            var uniqueTexes = new List<Texture2D> { new Texture2D(detectedSize, detectedSize) }; // Fallback at index 0
            var texToIndex = new Dictionary<Texture2D, ushort> { { uniqueTexes[0], 0 } };

            // 1. Map Meshes to Deterministic IDs
            var uniqueMeshes = new List<MeshDataSO>();
            var meshToId = new Dictionary<MeshDataSO, ushort>();
            
            foreach (var so in allBlocks)
            {
                if (so.VoxelData != null && !meshToId.ContainsKey(so.VoxelData))
                {
                    meshToId[so.VoxelData] = (ushort)uniqueMeshes.Count;
                    uniqueMeshes.Add(so.VoxelData);
                }
            }

            // 2. Map Block Textures & Properties
            int maxId = 0;
            foreach (var so in allBlocks) if (so.Block.ID > maxId) maxId = so.Block.ID;

            var protos = new NativeArray<Block>(maxId + 1, Allocator.Persistent);
            foreach (var so in allBlocks)
            {
                Block b = so.Block;
                b.MeshID = so.VoxelData != null ? meshToId[so.VoxelData] : (ushort)0;
                b.BaseTextures = GetLayer(so, TextureType.Base, uniqueTexes, texToIndex);
                b.NormalTextures = GetLayer(so, TextureType.Normal, uniqueTexes, texToIndex);
                b.AOTextures = GetLayer(so, TextureType.AO, uniqueTexes, texToIndex);
                b.OverlayTextures = GetLayer(so, TextureType.Overlay, uniqueTexes, texToIndex);
                protos[b.ID] = b;
            }

            // 3. Create the massive Texture2DArray for all texture maps
            var texArray = new Texture2DArray(detectedSize, detectedSize, uniqueTexes.Count, uniqueTexes[0].format, true, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BlockTextures"
            };

            for (int i = 0; i < uniqueTexes.Count; i++)
            {
                if(uniqueTexes[i] != null)
                {
                    for (int mip = 0; mip < uniqueTexes[i].mipmapCount; mip++)
                        Graphics.CopyTexture(uniqueTexes[i], 0, mip, texArray, i, mip);
                }
            }
            texArray.Apply(false, true);
            material?.SetTexture("_Base_Map_Array", texArray);

            // 4. Send over natively converted meshes to ECS world registry
            var em = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;

            // Destroy and cleanly dispose of any previous instances of the registry
            var oldQ = em.CreateEntityQuery(Unity.Entities.ComponentType.ReadWrite<World.WorldBlockRegistrySingleton>());
            if (!oldQ.IsEmpty)
            {
                var old = oldQ.GetSingleton<World.WorldBlockRegistrySingleton>();
                if (old.Blocks.IsCreated) old.Blocks.Dispose();
                if (old.Meshes.IsCreated)
                {
                    for (int i = 0; i < old.Meshes.Length; i++)
                    {
                        if (old.Meshes[i].Vertices.IsCreated) old.Meshes[i].Vertices.Dispose();
                        if (old.Meshes[i].Triangles.IsCreated) old.Meshes[i].Triangles.Dispose();
                    }
                    old.Meshes.Dispose();
                }
                em.DestroyEntity(oldQ.GetSingletonEntity());
            }
            oldQ.Dispose();

            var nativeMeshes = new NativeArray<NativeVoxelMeshData>(uniqueMeshes.Count, Allocator.Persistent);
            for (int i = 0; i < uniqueMeshes.Count; i++)
            {
                nativeMeshes[i] = new NativeVoxelMeshData {
                    Vertices = new NativeArray<float3>(uniqueMeshes[i].MeshData.Vertices, Allocator.Persistent),
                    Triangles = new NativeArray<int4>(uniqueMeshes[i].MeshData.Triangles, Allocator.Persistent)
                };
            }

            var regEntity = em.CreateEntity();
            em.AddComponentData(regEntity, new World.WorldBlockRegistrySingleton { Blocks = protos, Meshes = nativeMeshes });
            
            Unity.Entities.World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<ChunkRenderSystem>()?.SetMaterial(material);
        }

        private NativeTexturesIDLayer GetLayer(BlockDataSo so, TextureType type, List<Texture2D> uniqueTexes, Dictionary<Texture2D, ushort> dict)
        {
            foreach (var l in so.TexturesLayer)
            {
                if (l.TextureType == type)
                {
                    return new NativeTexturesIDLayer {
                        Front = GetOrAdd(l.FrontTexture, uniqueTexes, dict), Back = GetOrAdd(l.BackTexture, uniqueTexes, dict),
                        Top = GetOrAdd(l.TopTexture, uniqueTexes, dict), Bottom = GetOrAdd(l.BottomTexture, uniqueTexes, dict),
                        Right = GetOrAdd(l.RightTexture, uniqueTexes, dict), Left = GetOrAdd(l.LeftTexture, uniqueTexes, dict)
                    };
                }
            }
            return default;
        }

        private static ushort GetOrAdd(Texture2D tex, List<Texture2D> uniqueTexes, Dictionary<Texture2D, ushort> dict)
        {
            if (tex == null) return 0;
            if (dict.TryGetValue(tex, out ushort idx)) return idx;
            idx = (ushort)uniqueTexes.Count;
            uniqueTexes.Add(tex);
            dict[tex] = idx;
            return idx;
        }
    }
}
*/