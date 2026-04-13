using _Project.Tags;
using _Project.WorldGeneration.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.WorldGeneration.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ChunkRenderUploadSystem : SystemBase
    {
        private static readonly int verticesPid    = Shader.PropertyToID("vertices");
        private static readonly int chunkOriginPid = Shader.PropertyToID("chunk_origin");
        private static readonly int vertexCountPid = Shader.PropertyToID("vertex_count");

        private EntityQuery uploadQuery;

        protected override void OnCreate()
        {
            uploadQuery = SystemAPI.QueryBuilder()
                .WithAll<MeshRequiresUpload, ChunkMeshData, ChunkPositionComponent>()
                .Build();
        }

        protected override void OnUpdate()
        {
            if (uploadQuery.IsEmpty) return;

            // Must complete jobs writing ChunkMeshData before reading on main thread
            EntityManager.CompleteDependencyBeforeRW<ChunkMeshData>();

            // Snapshot entities BEFORE any structural change
            var entities = uploadQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                if (!EntityManager.HasComponent<ChunkMeshData>(entity)) continue;
                var meshData = EntityManager.GetComponentData<ChunkMeshData>(entity);

                ChunkGfxBuffers gfx;
                bool isNew = !EntityManager.HasComponent<ChunkGfxBuffers>(entity);
                if (isNew)
                {
                    gfx = new ChunkGfxBuffers { Mpb = new MaterialPropertyBlock() };
                    // Safe here: we are NOT iterating a query, just a plain array
                    EntityManager.AddComponentObject(entity, gfx);
                }
                else
                {
                    gfx = EntityManager.GetComponentObject<ChunkGfxBuffers>(entity);
                    if (gfx.Mpb == null) gfx.Mpb = new MaterialPropertyBlock();
                }

                int totalV  = meshData.CombinedVertices.Length;
                int totalI  = meshData.CombinedIndices.Length;
                int siCount = meshData.SolidIndexCount;
                int fiCount = totalI - siCount;

                if (totalV > 0)
                {
                    bool rebindVertex = false;
                    if (gfx.VertexBuffer == null || gfx.VertexBuffer.count < totalV)
                    {
                        gfx.VertexBuffer?.Release();
                        gfx.VertexBuffer = new GraphicsBuffer(
                            GraphicsBuffer.Target.Structured,
                            math.ceilpow2(totalV),
                            System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vertex)));
                        rebindVertex = true;
                    }
                    gfx.VertexBuffer.SetData(meshData.CombinedVertices.AsArray(), 0, 0, totalV);

                    if (gfx.IndexBuffer == null || gfx.IndexBuffer.count < totalI)
                    {
                        gfx.IndexBuffer?.Release();
                        gfx.IndexBuffer = new GraphicsBuffer(
                            GraphicsBuffer.Target.Index,
                            math.ceilpow2(totalI),
                            sizeof(int));
                    }
                    gfx.IndexBuffer.SetData(meshData.CombinedIndices.AsArray(), 0, 0, totalI);

                    if (gfx.ArgsBuffer == null)
                        gfx.ArgsBuffer = new GraphicsBuffer(
                            GraphicsBuffer.Target.IndirectArguments,
                            2,
                            GraphicsBuffer.IndirectDrawIndexedArgs.size);

                    var args = new GraphicsBuffer.IndirectDrawIndexedArgs[2];
                    args[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = (uint)siCount,
                        instanceCount         = 1,
                        startIndex            = 0
                    };
                    args[1] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = (uint)fiCount,
                        instanceCount         = 1,
                        startIndex            = (uint)siCount
                    };
                    gfx.ArgsBuffer.SetData(args);

                    gfx.SolidIndexCount = siCount;
                    gfx.FluidIndexCount = fiCount;

                    // Bind MPB once per (re)alloc. Buffer ref is stable until next Release.
                    if (rebindVertex || isNew)
                    {
                        gfx.Mpb.SetBuffer(verticesPid, gfx.VertexBuffer);
                        gfx.Mpb.SetInteger(vertexCountPid, gfx.VertexBuffer.count);

                        var wPos = EntityManager.GetComponentData<ChunkPositionComponent>(entity).WorldPosition;
                        gfx.Mpb.SetVector(chunkOriginPid, new Vector4(wPos.x, wPos.y, wPos.z, 0f));
                    }
                }
                else
                {
                    gfx.SolidIndexCount = 0;
                    gfx.FluidIndexCount = 0;
                }

                ecb.RemoveComponent<MeshRequiresUpload>(entity);
                if (!EntityManager.HasComponent<HasMesh>(entity))
                    ecb.AddComponent<HasMesh>(entity);

                // Chunk now needs collider rebake
                if (!EntityManager.HasComponent<NeedsColliderSync>(entity))
                    ecb.AddComponent<NeedsColliderSync>(entity);
            }

            entities.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}