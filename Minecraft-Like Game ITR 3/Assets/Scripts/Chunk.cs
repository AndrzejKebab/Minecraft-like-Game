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
			MeshVertices = new NativeList<half4>(Allocator.Persistent),
			MeshTriangles = new NativeList<ushort>(Allocator.Persistent),
			Normals = new NativeList<int3>(Allocator.Persistent),
			Tangents = new NativeList<int3>(Allocator.Persistent),
			MeshUVs = new NativeList<float2>(Allocator.Persistent)
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

		//mesh.subMeshCount = 1;

		var _layout = new[]
{
			new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4)
		};

		mesh.SetVertexBufferParams(meshData.MeshVertices.Length, _layout);
		mesh.SetVertexBufferData(meshData.MeshVertices.ToArray(), 0, 0, meshData.MeshVertices.Length, stream:0); ;

		mesh.SetIndexBufferParams(meshData.MeshTriangles.Length, IndexFormat.UInt16);
		mesh.SetIndexBufferData(meshData.MeshTriangles.AsArray(), 0, 0, meshData.MeshTriangles.Length);

		mesh.SetNormals(meshData.Normals.ToArray().Select(normal => new Vector3(normal.x, normal.y, normal.z)).ToArray());
		mesh.SetTangents(meshData.Tangents.ToArray().Select(tangents => new Vector4(tangents.x, tangents.y, tangents.z, 0)).ToArray());
		mesh.SetUVs(0, meshData.MeshUVs.ToArray().Select(uvs => new Vector2(uvs.x, uvs.y)).ToArray());

		var desc = new SubMeshDescriptor(0, meshData.MeshTriangles.Length, MeshTopology.Quads);
		mesh.SetSubMesh(0, desc);

		mesh.RecalculateUVDistributionMetrics();
		//mesh.Optimize();

		meshFilter.sharedMesh = mesh;
		meshCollider.sharedMesh = mesh;

		meshData.MeshTriangles.Dispose();
		meshData.MeshVertices.Dispose();
		meshData.Normals.Dispose();
		meshData.MeshUVs.Dispose();
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
			};
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
		meshData.MeshTriangles.Dispose();
		meshData.MeshVertices.Dispose();
		meshData.Normals.Dispose();
		meshData.MeshUVs.Dispose();
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