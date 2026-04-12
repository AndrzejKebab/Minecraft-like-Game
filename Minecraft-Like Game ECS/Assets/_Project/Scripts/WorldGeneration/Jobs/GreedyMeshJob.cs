using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using _Project.WorldGeneration.Blocks;

namespace _Project.WorldGeneration.Jobs
{
    [BurstCompile]
    public struct GreedyMeshJob : IJob
    {
        [ReadOnly] public ChunkAccessor Accessor;
        [ReadOnly] public NativeArray<Block> BlockPrototypes;
        
        public NativeMesh SolidMesh;
        public NativeMesh FluidMesh;

        private struct Mask
        {
            public ushort BlockID;
            public byte MeshType; // 0=Air, 1=Solid, 2=Fluid
            public sbyte Normal;
            public int4 AO;
        }

        public void Execute()
        {
            var chunkSize = Accessor.ChunkSize;

            for (var direction = 0; direction < 3; direction++)
            {
                var axis1 = (direction + 1) % 3;
                var axis2 = (direction + 2) % 3;

                int3 deltaAxis1 = int3.zero;
                int3 deltaAxis2 = int3.zero;
                int3 chunkItr = int3.zero;
                int3 directionMask = int3.zero;
                directionMask[direction] = 1;

                var normalMask = new NativeArray<Mask>(chunkSize * chunkSize, Allocator.Temp);

                for (chunkItr[direction] = -1; chunkItr[direction] < chunkSize;)
                {
                    var n = 0;

                    // 1. Compute Masks
                    for (chunkItr[axis2] = 0; chunkItr[axis2] < chunkSize; chunkItr[axis2]++)
                    {
                        for (chunkItr[axis1] = 0; chunkItr[axis1] < chunkSize; chunkItr[axis1]++)
                        {
                            var currentBlock = Accessor.GetBlock(chunkItr);
                            var compareBlock = Accessor.GetBlock(chunkItr + directionMask);

                            var currentType = GetMeshType(currentBlock);
                            var compareType = GetMeshType(compareBlock);

                            if (currentType == compareType)
                            {
                                normalMask[n++] = default; // Same type, internal face -> cull
                            }
                            else if (currentType < compareType)
                            {
                                normalMask[n++] = new Mask { 
                                    BlockID = currentBlock, MeshType = currentType, Normal = 1, 
                                    AO = ComputeAO(chunkItr + directionMask, axis1, axis2) 
                                };
                            }
                            else
                            {
                                normalMask[n++] = new Mask { 
                                    BlockID = compareBlock, MeshType = compareType, Normal = -1, 
                                    AO = ComputeAO(chunkItr, axis1, axis2) 
                                };
                            }
                        }
                    }

                    chunkItr[direction]++;
                    n = 0;

                    // 2. Greedy Face Merging
                    for (var j = 0; j < chunkSize; j++)
                    {
                        for (var i = 0; i < chunkSize;)
                        {
                            if (normalMask[n].MeshType != 0) // Not Air
                            {
                                Mask currentMask = normalMask[n];
                                chunkItr[axis1] = i;
                                chunkItr[axis2] = j;

                                // Compute Width
                                int width;
                                for (width = 1; i + width < chunkSize && CompareMask(normalMask[n + width], currentMask); width++) { }

                                // Compute Height
                                int height;
                                var done = false;
                                for (height = 1; j + height < chunkSize; height++)
                                {
                                    for (var k = 0; k < width; k++)
                                    {
                                        if (CompareMask(normalMask[n + k + height * chunkSize], currentMask)) continue;
                                        done = true; break;
                                    }
                                    if (done) break;
                                }

                                deltaAxis1[axis1] = width;
                                deltaAxis2[axis2] = height;

                                // Generate Quad
                                CreateQuad(currentMask, direction, directionMask, width, height, chunkItr, chunkItr + deltaAxis1, chunkItr + deltaAxis2, chunkItr + deltaAxis1 + deltaAxis2);

                                // Clear Mask
                                for (var l = 0; l < height; l++)
                                    for (var k = 0; k < width; k++)
                                        normalMask[n + k + l * chunkSize] = default;

                                i += width;
                                n += width;
                            }
                            else
                            {
                                i++;
                                n++;
                            }
                        }
                    }
                }
                normalMask.Dispose();
            }
        }

        private bool CompareMask(Mask a, Mask b)
        {
            return a.MeshType == b.MeshType && 
                   a.BlockID == b.BlockID && 
                   a.Normal == b.Normal && 
                   a.AO.Equals(b.AO); // Must have same AO to merge smoothly!
        }

        private byte GetMeshType(ushort blockId)
        {
            if (blockId == 0) return 0; // Air
            // Add custom fluid ID logic here
            return 1; // Solid
        }

        // Implementation of AO check taking advantage of the unified accessor
        private int4 ComputeAO(int3 pos, int axis1, int axis2)
        {
            // Vloxy Engine algorithm: Checks neighbors to output an int4 containing AO factors (0-3) for the 4 corners of a face.
            // Simplified version here: return int4(3,3,3,3) for fully lit if you want to skip AO initially.
            return new int4(3, 3, 3, 3); 
        }

        private void CreateQuad(Mask mask, int direction, int3 directionMask, int width, int height, int3 v1, int3 v2, int3 v3, int3 v4)
        {
            NativeMesh mesh = mask.MeshType == 1 ? SolidMesh : FluidMesh;
            var vertexCount = mesh.Vertices.Length;
            
            var normalIdx = direction * 2 + (mask.Normal > 0 ? 1 : 0);

            // Pack your Vertices
            mesh.Vertices.Add(PackVertex(v1, 0, 0, mask, normalIdx, mask.AO.x));
            mesh.Vertices.Add(PackVertex(v2, width, 0, mask, normalIdx, mask.AO.y));
            mesh.Vertices.Add(PackVertex(v3, 0, height, mask, normalIdx, mask.AO.z));
            mesh.Vertices.Add(PackVertex(v4, width, height, mask, normalIdx, mask.AO.w));

            // Standard quad indexing (+ - direction flips order)
            if (mask.Normal > 0) {
                mesh.Triangles.Add(vertexCount);
                mesh.Triangles.Add(vertexCount + 2);
                mesh.Triangles.Add(vertexCount + 1);
                mesh.Triangles.Add(vertexCount + 1);
                mesh.Triangles.Add(vertexCount + 2);
                mesh.Triangles.Add(vertexCount + 3);
            } else {
                mesh.Triangles.Add(vertexCount);
                mesh.Triangles.Add(vertexCount + 1);
                mesh.Triangles.Add(vertexCount + 2);
                mesh.Triangles.Add(vertexCount + 1);
                mesh.Triangles.Add(vertexCount + 3);
                mesh.Triangles.Add(vertexCount + 2);
            }
        }

        private static Vertex PackVertex(int3 pos, int u, int v, Mask mask, int normalIdx, int ao)
        {
            var vertex = new Vertex
                         {
                             // Pack Data1: PosX(6), PosY(6), PosZ(6), UV_X(6), UV_Y(6), AO(2)
                             Data1 = ((uint)pos.x & 0x3F) |
                                     (((uint)pos.y & 0x3F) << 6) |
                                     (((uint)pos.z & 0x3F) << 12) |
                                     (((uint)u & 0x3F) << 18) |
                                     (((uint)v & 0x3F) << 24) |
                                     (((uint)ao & 0x3) << 30)
                         };
            
            return vertex;
        }
    }
}