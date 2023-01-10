using System.Collections.Generic;
using UnityEngine;

public class Chunk 
{
	[SerializeField] private ChunkCoord coord;

	private GameObject chunkObject;
	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;

	private int vertexIndex = 0;
	private List<Vector3> vertices = new List<Vector3> ();
	private List<int> triangles = new List<int> ();
	private List<int> transparentTriangles = new List<int>();
	private Material[] materials = new Material[2];
	private List<Vector2> uvs = new List<Vector2> ();

	public byte[,,] VoxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

	public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

	private World world;

	private bool _isActive;
	public bool IsVoxelMapPopulated = false;

	public Chunk (ChunkCoord _coord, World _world, bool generateOnLoad) 
	{
		coord = _coord;
		world = _world;
		isActive = true;

		if (generateOnLoad)
			Init();
	}

	public void Init () 
	{
		chunkObject = new GameObject();
		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();

		materials[0] = world.Material;
		materials[1] = world.TransparentMaterial;
		meshRenderer.materials = materials;

		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(coord.x * VoxelData.ChunkWidth, 0f, coord.z * VoxelData.ChunkWidth);
		chunkObject.name = "Chunk " + coord.x + ", " + coord.z;

		PopulateVoxelMap();
		UpdateChunk();
	}

	private void PopulateVoxelMap () 
	{
		for (int y = 0; y < VoxelData.ChunkHeight; y++) 
		{
			for (int x = 0; x < VoxelData.ChunkWidth; x++) 
			{
				for (int z = 0; z < VoxelData.ChunkWidth; z++) 
				{
					VoxelMap[x, y, z] = world.GetVoxel(new Vector3(x, y, z) + position);
				}
			}
		}

		IsVoxelMapPopulated = true;

	}

	public void UpdateChunk () 
	{
		while(modifications.Count > 0)
		{
			VoxelMod v = modifications.Dequeue();
			Vector3 pos = v.Position -= position;
			VoxelMap[(int)pos.x, (int)pos.y, (int)pos.z] = v.ID;
		}

		ClearMeshData();

		for (int y = 0; y < VoxelData.ChunkHeight; y++) 
		{
			for (int x = 0; x < VoxelData.ChunkWidth; x++) 
			{
				for (int z = 0; z < VoxelData.ChunkWidth; z++)
				{
					if (world.BlockTypes[VoxelMap[x,y,z]].IsSolid)
						UpdateMeshData (new Vector3(x, y, z));
				}
			}
		}
		CreateMesh();
	}

	private void ClearMeshData()
	{
		vertexIndex = 0;
		vertices.Clear();
		triangles.Clear();
		transparentTriangles.Clear();
		uvs.Clear();
	}

	public bool isActive 
	{
		get { return _isActive; }
		set {
			_isActive = value;
			if (chunkObject != null)
				chunkObject.SetActive(value);
		}
	}

	public Vector3 position
	{
		get { return chunkObject.transform.position; }
	}

	private bool IsVoxelInChunk (int x, int y, int z) 
	{
		if (x < 0 || x > VoxelData.ChunkWidth - 1 || y < 0 || y > VoxelData.ChunkHeight - 1 || z < 0 || z > VoxelData.ChunkWidth - 1)
		{
			return false;
		}
		else
		{
			return true;
		}
	}

	public void EditVoxel(Vector3 pos, byte newID)
	{
		int xCheck = Mathf.FloorToInt(pos.x);
		int yCheck = Mathf.FloorToInt(pos.y);
		int zCheck = Mathf.FloorToInt(pos.z);

		xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
		zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

		VoxelMap[xCheck, yCheck, zCheck] = newID;

		UpdateSurroundingVoxel(xCheck, yCheck, zCheck);
		UpdateChunk();
	}

	private void UpdateSurroundingVoxel(int x, int y, int z)
	{
		Vector3 thisVoxel = new Vector3(x, y, z);

		for(int p = 0; p < 6; p++)
		{
			Vector3 currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

			if (!IsVoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z))
			{
				world.GetChunkFromVector3(currentVoxel + position).UpdateChunk();
			}
		}
	}

	private bool CheckVoxel (Vector3 pos) 
	{
		int x = Mathf.FloorToInt (pos.x);
		int y = Mathf.FloorToInt (pos.y);
		int z = Mathf.FloorToInt (pos.z);

		if (!IsVoxelInChunk(x, y, z)) return world.CheckIfVoxelTransparent(pos + position);

		return world.BlockTypes[VoxelMap [x, y, z]].IsTransparent;
	}

	public byte GetVoxelFromGlobalVector3 (Vector3 pos) 
	{
		int xCheck = Mathf.FloorToInt(pos.x);
		int yCheck = Mathf.FloorToInt(pos.y);
		int zCheck = Mathf.FloorToInt(pos.z);

		xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
		zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

		return VoxelMap[xCheck, yCheck, zCheck];
	}

	private void UpdateMeshData (Vector3 pos) 
	{
		byte BlockID = VoxelMap[(int)pos.x, (int)pos.y, (int)pos.z];
		bool _isTransparent = world.BlockTypes[BlockID].IsTransparent;

		for (int p = 0; p < 6; p++) 
		{ 
			if (CheckVoxel(pos + VoxelData.FaceChecks[p])) 
			{
				vertices.Add (pos + VoxelData.VoxelVerticles [VoxelData.VoxelTriangles [p, 0]]);
				vertices.Add (pos + VoxelData.VoxelVerticles [VoxelData.VoxelTriangles [p, 1]]);
				vertices.Add (pos + VoxelData.VoxelVerticles [VoxelData.VoxelTriangles [p, 2]]);
				vertices.Add (pos + VoxelData.VoxelVerticles [VoxelData.VoxelTriangles [p, 3]]);

				AddTexture(world.BlockTypes[BlockID].GetTextureID(p));

				if(!_isTransparent)
				{
					triangles.Add(vertexIndex);
					triangles.Add(vertexIndex + 1);
					triangles.Add(vertexIndex + 2);
					triangles.Add(vertexIndex + 2);
					triangles.Add(vertexIndex + 1);
					triangles.Add(vertexIndex + 3);
				}
				else
				{
					transparentTriangles.Add(vertexIndex);
					transparentTriangles.Add(vertexIndex + 1);
					transparentTriangles.Add(vertexIndex + 2);
					transparentTriangles.Add(vertexIndex + 2);
					transparentTriangles.Add(vertexIndex + 1);
					transparentTriangles.Add(vertexIndex + 3);
				}

				vertexIndex += 4;
			}
		}
	}

	private void CreateMesh () 
	{
		Mesh mesh = new Mesh ();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		mesh.vertices = vertices.ToArray ();
		mesh.subMeshCount = 2;
		mesh.SetTriangles(triangles.ToArray(), 0);
		mesh.SetTriangles(transparentTriangles.ToArray(), 1);
		mesh.uv = uvs.ToArray ();

		mesh.RecalculateNormals ();

		meshFilter.mesh = mesh;
	}

	private void AddTexture (int textureID)
	{
		float y = textureID / VoxelData.TextureAtlasSizeInBlocks;
		float x = textureID - (y * VoxelData.TextureAtlasSizeInBlocks);

		x *= VoxelData.NormalizedBlockTextureSize;
		y *= VoxelData.NormalizedBlockTextureSize;

		y = 1f - y - VoxelData.NormalizedBlockTextureSize;

		uvs.Add(new Vector2(x, y));
		uvs.Add(new Vector2(x, y + VoxelData.NormalizedBlockTextureSize));
		uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y));
		uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y + VoxelData.NormalizedBlockTextureSize));
	}
}

public class ChunkCoord {

	public int x;
	public int z;

	public ChunkCoord () 
	{
		x = 0;
		z = 0;
	}

	public ChunkCoord (int _x, int _z) 
	{
		x = _x;
		z = _z;
	}

	public ChunkCoord (Vector3 pos) 
	{
		int xCheck = Mathf.FloorToInt(pos.x);
		int zCheck = Mathf.FloorToInt(pos.z);

		x = xCheck / VoxelData.ChunkWidth;
		z = zCheck / VoxelData.ChunkWidth;
	}

	public bool Equals (ChunkCoord other)
	{
		if (other == null)
		{
			return false;
		}
		else if (other.x == x && other.z == z)
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}
