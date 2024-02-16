using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static PopulateVoxelMapJob;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class Chunk
{
    private GameObject chunkObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private readonly Mesh mesh = new();

    private int3 Coord { get; }
    private readonly World world;
    private bool isActive;

    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            if (chunkObject != null) chunkObject.SetActive(value);
        }
    }

    public bool IsScheduled { get; private set; }
    public bool IsMeshDataCompleted => chunkJobHandle.IsCompleted;
    public bool IsVoxelMapCompleted => populateVoxelMapHandle.IsCompleted;

    public bool VoxelMapPopulated;
    public bool IsUpdating = true;

    private float3 ChunkPosition { get; set; }

    private NativeArray<ushort> voxelMap =
        new((int)Mathf.Pow(VoxelData.ChunkSize, 3), Allocator.Persistent);

    private JobHandle chunkJobHandle;
    private ChunkJob.MeshData meshData;
    private JobHandle populateVoxelMapHandle;
    private VoxelMapData voxelMapData;
    
    private readonly VertexAttributeDescriptor[] layout =
    {
        new(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
        new(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
        new(VertexAttribute.Tangent, VertexAttributeFormat.UNorm8, 4),
        new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2)
    };

    public Chunk(int3 coord, World world)
    {
        Coord = coord;
        this.world = world;
        isActive = true;
    }

    public void Initialise()
    {
        IsScheduled = true;
        chunkObject = new GameObject();

        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshRenderer.material = world.Material;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(Coord.x * VoxelData.ChunkSize, Coord.y * VoxelData.ChunkSize,
            Coord.z * VoxelData.ChunkSize);
        chunkObject.name = $"Chunk [{Coord.x}, {Coord.y}, {Coord.z}]";
        chunkObject.layer = LayerMask.NameToLayer("Chunk");

        ChunkPosition = chunkObject.transform.position;

        PopulateVoxelMap();
    }

    private void PopulateVoxelMap()
    {
        voxelMapData = new VoxelMapData
        {
            ChunkSize = VoxelData.ChunkSize,
            WorldSizeInVoxels = VoxelData.WorldSizeInVoxels,
            BiomeData = world.BiomeAttributesJob
        };

        populateVoxelMapHandle = new PopulateVoxelMapJob
        {
            Position = ChunkPosition,
            VoxelData = voxelMapData,
            VoxelMap = voxelMap
        }.Schedule();
        CreateMeshDataJob();
    }

    private void CreateMeshDataJob()
    {
        //populateVoxelMapHandle.Complete();
        VoxelMapPopulated = true;

        IsUpdating = true;
        meshData = new ChunkJob.MeshData
        {
            Vertex = new NativeList<Vertex>(Allocator.Persistent),
            MeshTriangles = new NativeList<ushort>(Allocator.Persistent)
        };

        chunkJobHandle = new ChunkJob
        {
            meshData = meshData,
            chunkData = new ChunkJob.ChunkData
            {
                VoxelMap = voxelMap,
                BlockTypes = world.BlockTypesJobs,
                BiomeData = world.BiomeAttributesJob
            },

            ChunkSize = VoxelData.ChunkSize,
            TextureAtlasSize = VoxelData.TextureAtlasSizeInBlocks,
            NormalizedTextureAtlas = VoxelData.NormalizedBlockTextureSize,
            Position = new int3(ChunkPosition),
            WorldSizeInVoxels = VoxelData.WorldSizeInVoxels
        }.Schedule(populateVoxelMapHandle);
    }

    public void CreateMesh()
    {
        chunkJobHandle.Complete();
        mesh.Clear();
        mesh.name = "Chunk";
        mesh.MarkDynamic();
        mesh.bounds = world.ChunkBound;
        mesh.subMeshCount = 1;

        NativeArray<Vertex> vertexArray = meshData.Vertex.AsArray();
        NativeArray<ushort> triangleArray = meshData.MeshTriangles.AsArray();

        mesh.SetVertexBufferParams(vertexArray.Length, layout);
        mesh.SetVertexBufferData(vertexArray, 0, 0, meshData.Vertex.Length, 0);

        mesh.SetIndexBufferParams(triangleArray.Length, IndexFormat.UInt16);
        mesh.SetIndexBufferData(triangleArray, 0, 0, meshData.MeshTriangles.Length);

        var desc = new SubMeshDescriptor(0, meshData.MeshTriangles.Length, MeshTopology.Quads);
        mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontRecalculateBounds);

        mesh.RecalculateUVDistributionMetrics();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;

        meshData.Vertex.Dispose();
        meshData.MeshTriangles.Dispose();
        IsUpdating = false;
    }

    public void EditVoxel(int3 pos, ushort blockId)
    {
        var xCheck = Mathf.FloorToInt(pos.x);
        var yCheck = Mathf.FloorToInt(pos.y);
        var zCheck = Mathf.FloorToInt(pos.z);

        var position = chunkObject.transform.position;
        xCheck -= Mathf.FloorToInt(position.x);
        yCheck -= Mathf.FloorToInt(position.y);
        zCheck -= Mathf.FloorToInt(position.z);

        voxelMap[WorldExtensions.FlattenIndex(xCheck, yCheck, zCheck, VoxelData.ChunkSize)] = blockId;

        UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
        CreateMeshDataJob();
        CreateMesh();
    }

    private void UpdateSurroundingVoxels(int x, int y, int z)
    {
        var thisVoxel = new int3(x, y, z);

        for (var p = 0; p < 6; p++)
        {
            var currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

            if (!IsVoxelInChunk(currentVoxel))
                world.GetChunkFromVector3(math.float3(currentVoxel + ChunkPosition)).CreateMeshDataJob();
        }
    }

    public ushort GetVoxelFromGlobalVector3(Vector3 pos)
    {
        var xCheck = Mathf.FloorToInt(pos.x);
        var yCheck = Mathf.FloorToInt(pos.y);
        var zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(ChunkPosition.x);
        yCheck -= Mathf.FloorToInt(ChunkPosition.y);
        zCheck -= Mathf.FloorToInt(ChunkPosition.z);

        return voxelMap[WorldExtensions.FlattenIndex(xCheck, yCheck, zCheck, VoxelData.ChunkSize)];
    }

    public void OnDestroy()
    {
        chunkJobHandle.Complete();

        voxelMap.Dispose();
        meshData.Vertex.Dispose();
        meshData.MeshTriangles.Dispose();
        ;
        IsUpdating = false;

        Object.Destroy(chunkObject.GetComponent<MeshCollider>().sharedMesh);
        Object.Destroy(chunkObject.GetComponent<MeshFilter>().sharedMesh);
        Object.Destroy(chunkObject.GetComponent<MeshRenderer>().material);
        Object.Destroy(chunkObject);
    }

    private static bool IsVoxelInChunk(int3 pos)
    {
        return pos.x is >= 0 and <= VoxelData.ChunkSize - 1 &&
               pos.y is >= 0 and <= VoxelData.ChunkSize - 1 &&
               pos.z is >= 0 and <= VoxelData.ChunkSize - 1;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public half4 Position;
    public sbyte4 Normal;
    public Color32 Color;
    public half2 UVs;

    public Vertex(half4 position, sbyte4 normal, Color32 color, half2 uv)
    {
        Position = position;
        Normal = normal;
        Color = color;
        UVs = uv;
    }
}

#pragma warning disable 0659
[Serializable]
public struct sbyte4 : IEquatable<sbyte4>, IFormattable
{
    public sbyte x, y, z, w;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte4(sbyte x, sbyte y, sbyte z, sbyte w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(sbyte4 rhs)
    {
        return x == rhs.x && y == rhs.y && z == rhs.z && w == rhs.w;
    }

    public override bool Equals(object o)
    {
        return o is sbyte4 converted && Equals(converted);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        return string.Format("sbyte4({0}, {1}, {2}, {3})", x, y, z, w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string format, IFormatProvider formatProvider)
    {
        return string.Format("sbyte4({0}, {1}, {2}, {3})", x.ToString(format, formatProvider),
            y.ToString(format, formatProvider), z.ToString(format, formatProvider), w.ToString(format, formatProvider));
    }
}