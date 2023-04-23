using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static PopulateVoxelMapJob;

public class Chunk
{
	private GameObject chunkObject;
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;
	private MeshCollider meshCollider;
	private Mesh mesh = new Mesh();

	public int3 Coord { get; private set; }
	private World world;

	private bool isActive = false;
	public bool IsActive
	{
		get
		{
			return isActive;
		}
		set
		{
			isActive = value;
			if (chunkObject != null)
			{
				chunkObject.SetActive(value);
			}
		}
	}
	private bool isScheduled = false;
	public bool IsScheduled { get { return isScheduled;} }
	public bool IsCompleted { get { return chunkJobHandle.IsCompleted; } }
	public bool IsUpdating = true;

	public float3 ChunkPosition { get; private set; }

	private NativeArray<ushort> voxelMap = new NativeArray<ushort>((int)Mathf.Pow(VoxelData.ChunkSize, 3), Allocator.Persistent);

	private JobHandle chunkJobHandle;
	private ChunkJob.MeshData meshData;
	private JobHandle populateVoxelMapHandle;
	private VoxelMapData voxelMapData;

	public Chunk(int3 Coord, World world)
	{
		this.Coord = Coord;
		this.world = world;
		isActive = true;
	}

	public void Initialise()
	{
		isScheduled = true;
		chunkObject = new GameObject();

		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();
		meshCollider = chunkObject.AddComponent<MeshCollider>();
		meshRenderer.material = world.Material;

		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(Coord.x * VoxelData.ChunkSize, Coord.y * VoxelData.ChunkSize, Coord.z * VoxelData.ChunkSize);
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
			BiomeData = world.biomeAttributesJob
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
		IsUpdating = true;
		meshData = new ChunkJob.MeshData()
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
				BlockTypes = world.blockTypesJobs,
				BiomeData = world.biomeAttributesJob
			},

			blockData = new ChunkJob.BlockData
			{
				BlockVertices = VoxelData.VoxelVertices,
				BlockTriangles = VoxelData.VoxelTriangles,
				BlockUVs = VoxelData.VoxelUVs,
				BlockFaceChecks = VoxelData.FaceChecks
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

		var _layout = new[]
{
			new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
			new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
			new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.UNorm8, 4),
			new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2)
		};

		mesh.SetVertexBufferParams(meshData.Vertex.Length, _layout);
		mesh.SetVertexBufferData(meshData.Vertex.ToArray(), 0, 0, meshData.Vertex.Length, stream:0);

		mesh.SetIndexBufferParams(meshData.MeshTriangles.Length, IndexFormat.UInt16);
		mesh.SetIndexBufferData(meshData.MeshTriangles.AsArray(), 0, 0, meshData.MeshTriangles.Length);

		var desc = new SubMeshDescriptor(0, meshData.MeshTriangles.Length, MeshTopology.Quads);
		mesh.SetSubMesh(0, desc);

		mesh.RecalculateUVDistributionMetrics();

		meshFilter.sharedMesh = mesh;
		meshCollider.sharedMesh = mesh;

		meshData.Vertex.Dispose();
		meshData.MeshTriangles.Dispose();
		IsUpdating = false;
	}

	public void EditVoxel(int3 pos, ushort blockId)
	{
		int xCheck = Mathf.FloorToInt(pos.x);
		int yCheck = Mathf.FloorToInt(pos.y);
		int zCheck = Mathf.FloorToInt(pos.z);

		xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
		yCheck -= Mathf.FloorToInt(chunkObject.transform.position.y);
		zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

		voxelMap[WorldExtensions.FlattenIndex(xCheck, yCheck, zCheck, VoxelData.ChunkSize)] = blockId;

		UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
		CreateMeshDataJob();
		CreateMesh();
	}

	private void UpdateSurroundingVoxels(int x, int y, int z)
	{
		int3 thisVoxel = new int3(x, y, z);

		for(int p = 0; p < 6; p++)
		{
			int3 currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

			if (!IsVoxelInChunk(currentVoxel))
			{
				world.GetChunkFromVector3(math.float3(currentVoxel + ChunkPosition)).CreateMeshDataJob();
			}
		}
	}

	public ushort GetVoxelFromGlobalVector3(Vector3 pos)
	{
		int xCheck = Mathf.FloorToInt(pos.x);
		int yCheck = Mathf.FloorToInt(pos.y);
		int zCheck = Mathf.FloorToInt(pos.z);

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
		meshData.MeshTriangles.Dispose();;
		IsUpdating = false;

		GameObject.Destroy(chunkObject.GetComponent<MeshCollider>().sharedMesh);
		GameObject.Destroy(chunkObject.GetComponent<MeshFilter>().sharedMesh);
		GameObject.Destroy(chunkObject.GetComponent<MeshRenderer>().material);
		GameObject.Destroy(chunkObject);
	}

	private bool IsVoxelInChunk(int3 pos)
	{
		if (pos.x < 0 || pos.x > VoxelData.ChunkSize - 1 ||
			pos.y < 0 || pos.y > VoxelData.ChunkSize - 1 ||
			pos.z < 0 || pos.z > VoxelData.ChunkSize - 1)
		{
			return false;
		}
		else
		{
			return true;
		}
	}
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct Vertex
{
	public half4 Position;
	public half4 Normal;
	public Color32 Color;
	public half2 UVs;

	public Vertex(half4 position, half4 normal, Color32 color, half2 uv)
	{
		Position = position;
		Normal = normal;
		Color = color;
		UVs = uv;
	}
}