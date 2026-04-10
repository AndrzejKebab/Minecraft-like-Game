using _Project.WorldGeneration.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.WorldGeneration.Jobs
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance,
                  FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public struct BuildMeshJob : IJob
    {
        [ReadOnly] public NativeArray<BlockState> Blocks;
        [ReadOnly] public NativeArray<Block>      BlockPrototypes;

        [NativeDisableContainerSafetyRestriction] [ReadOnly]
        public NativeArray<NativeVoxelMeshData> Meshes;

        [ReadOnly] public NativeArray<BlockState> NeighborZNeg;
        [ReadOnly] public NativeArray<BlockState> NeighborZPos;
        [ReadOnly] public NativeArray<BlockState> NeighborYNeg;
        [ReadOnly] public NativeArray<BlockState> NeighborYPos;
        [ReadOnly] public NativeArray<BlockState> NeighborXNeg;
        [ReadOnly] public NativeArray<BlockState> NeighborXPos;

        [ReadOnly] public NativeArray<float3> FaceChecks;
        [ReadOnly] public NativeArray<float3> FaceTangents;
        public            int                 ChunkSize;

        // Pre-allocated by the system — write directly, never create NativeMesh inside Execute
        public NativeMesh SolidMesh;
        public NativeMesh FluidMesh;

        public void Execute()
        {
            if (Meshes.Length == 0) return;

            for (ushort x = 0; x < ChunkSize; x++)
            for (ushort y = 0; y < ChunkSize; y++)
            for (ushort z = 0; z < ChunkSize; z++)
            {
                BlockState blockState = Blocks[x | (y << 5) | (z << 10)];
                if (blockState.IsEmpty) continue;

                Block block = BlockPrototypes[blockState.ID];
                if (block.MeshID >= Meshes.Length) continue;

                NativeVoxelMeshData meshData = Meshes[block.MeshID];
                quaternion          rot      = GetRotation(block.DirectionType, blockState.Orientation);
                var                 wPos     = new float3(x, y, z);

                for (var i = 0; i < meshData.Triangles.Length; i++)
                {
                    int4   quad            = meshData.Triangles[i];
                    float3 originalNormal  = FaceChecks[i];
                    float3 originalTangent = FaceTangents[i];

                    float3 rotatedNormal = math.round(math.mul(rot, originalNormal));
                    var    dir           = new int3(rotatedNormal);

                    if (NeighbourHidesFace(x, y, z, dir, block.IsTransparent)) continue;

                    // Rotate each vertex around the block centre then shift to chunk space.
                    // Works for any mesh shape — cube, slab, stair, custom.
                    float3 v0 = math.mul(rot, meshData.Vertices[quad.x] - 0.5f) + 0.5f + wPos;
                    float3 v1 = math.mul(rot, meshData.Vertices[quad.y] - 0.5f) + 0.5f + wPos;
                    float3 v2 = math.mul(rot, meshData.Vertices[quad.z] - 0.5f) + 0.5f + wPos;
                    float3 v3 = math.mul(rot, meshData.Vertices[quad.w] - 0.5f) + 0.5f + wPos;

                    float3 rotatedTangent    = math.round(math.mul(rot, originalTangent));
                    float3 originalBitangent = math.cross(originalNormal, originalTangent);
                    float3 rotatedBitangent  = math.round(math.mul(rot, originalBitangent));
                    float3 expectedBitangent = math.cross(rotatedNormal, rotatedTangent);
                    float  tangentW          = math.dot(rotatedBitangent, expectedBitangent) >= 0f ? 1f : -1f;
                    var    tangent4          = new float4(rotatedTangent, tangentW);

                    ushort tBase     = GetTextureIndex(originalNormal, block.BaseTextures);
                    ushort tOverlay  = GetTextureIndex(originalNormal, block.OverlayTextures);
                    ushort tNormal   = GetTextureIndex(originalNormal, block.NormalTextures);
                    ushort tSpecular = GetTextureIndex(originalNormal, block.SpecularTextures);

                    Vertex vert0 = MakeVertex(v0, rotatedNormal, tangent4, block.TintColor, 0f, 0f, tBase, tOverlay, tNormal, tSpecular);
                    Vertex vert1 = MakeVertex(v1, rotatedNormal, tangent4, block.TintColor, 0f, 1f, tBase, tOverlay, tNormal, tSpecular);
                    Vertex vert2 = MakeVertex(v2, rotatedNormal, tangent4, block.TintColor, 1f, 0f, tBase, tOverlay, tNormal, tSpecular);
                    Vertex vert3 = MakeVertex(v3, rotatedNormal, tangent4, block.TintColor, 1f, 1f, tBase, tOverlay, tNormal, tSpecular);

                    if (block.IsFluid) AddFace(vert0, vert1, vert2, vert3, ref FluidMesh);
                    else               AddFace(vert0, vert1, vert2, vert3, ref SolidMesh);
                }
            }
        }

        private static Vertex MakeVertex(float3 pos, float3 norm, float4 tangent,
                                          Color32 color, float u, float v,
                                          ushort tBase, ushort tOverlay, ushort tNorm, ushort tSpec)
        {
            return new Vertex
            {
                Position   = new half4((half3)pos,  (half)0),
                Normal     = new half4((half3)norm, (half)0),
                Tangent    = new half4(tangent),
                Color      = color,
                UVs        = new half2((half)u, (half)v),
                TextureIDs = new half4((half)tBase, (half)tOverlay, (half)tNorm, (half)tSpec)
            };
        }

        private bool NeighbourHidesFace(int x, int y, int z, int3 dir, bool isTransparent)
        {
            int    nx = x + dir.x, ny = y + dir.y, nz = z + dir.z;
            ushort nbId = 0;

            if      (nx < 0)          { if (NeighborXNeg.IsCreated) nbId = NeighborXNeg[(ChunkSize-1) | (ny << 5) | (nz << 10)].ID; }
            else if (nx >= ChunkSize) { if (NeighborXPos.IsCreated) nbId = NeighborXPos[0             | (ny << 5) | (nz << 10)].ID; }
            else if (ny < 0)          { if (NeighborYNeg.IsCreated) nbId = NeighborYNeg[nx | ((ChunkSize-1) << 5) | (nz << 10)].ID; }
            else if (ny >= ChunkSize) { if (NeighborYPos.IsCreated) nbId = NeighborYPos[nx |                        (nz << 10)].ID; }
            else if (nz < 0)          { if (NeighborZNeg.IsCreated) nbId = NeighborZNeg[nx | (ny << 5) | ((ChunkSize-1) << 10)].ID; }
            else if (nz >= ChunkSize) { if (NeighborZPos.IsCreated) nbId = NeighborZPos[nx | (ny << 5)                        ].ID; }
            else                      { nbId = Blocks[nx | (ny << 5) | (nz << 10)].ID; }

            if (nbId == 0) return false;
            return !(BlockPrototypes[nbId].IsTransparent && !isTransparent);
        }

        private static void AddFace(Vertex v0, Vertex v1, Vertex v2, Vertex v3, ref NativeMesh mesh)
        {
            int b = mesh.Vertices.Length;
            mesh.Vertices.Add(v0);
            mesh.Vertices.Add(v1);
            mesh.Vertices.Add(v2);
            mesh.Vertices.Add(v3);
            mesh.Triangles.Add(b);
            mesh.Triangles.Add(b + 1);
            mesh.Triangles.Add(b + 3);
            mesh.Triangles.Add(b);
            mesh.Triangles.Add(b + 3);
            mesh.Triangles.Add(b + 2);
        }

        private static ushort GetTextureIndex(float3 normal, in NativeTexturesIDLayer layer)
        {
            if (normal.y >  0.5f) return layer.Top;
            if (normal.y < -0.5f) return layer.Bottom;
            if (normal.x >  0.5f) return layer.Right;
            if (normal.x < -0.5f) return layer.Left;
            if (normal.z >  0.5f) return layer.Front;
            return layer.Back;
        }

        private static quaternion GetRotation(BlockDirectionType type, byte orientation) =>
            type switch
            {
                BlockDirectionType.None => quaternion.identity,
                BlockDirectionType.YAxis => orientation switch
                {
                    2 => quaternion.identity,
                    4 => quaternion.Euler(0, math.PI / 2f, 0),
                    3 => quaternion.Euler(0, math.PI, 0),
                    5 => quaternion.Euler(0, -math.PI / 2f, 0),
                    _ => quaternion.identity
                },
                BlockDirectionType.AllAxes => orientation switch
                {
                    0 => quaternion.identity,
                    1 => quaternion.Euler(math.PI, 0, 0),
                    2 => quaternion.Euler(math.PI / 2f, 0, 0),
                    3 => quaternion.Euler(-math.PI / 2f, 0, 0),
                    4 => quaternion.Euler(0, 0, -math.PI / 2f),
                    5 => quaternion.Euler(0, 0, math.PI / 2f),
                    _ => quaternion.identity
                },
                _ => quaternion.identity
            };
    }
}