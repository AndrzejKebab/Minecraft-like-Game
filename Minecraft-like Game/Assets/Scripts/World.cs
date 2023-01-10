using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour 
{
	[Header("World Generation Data")]
	[SerializeField] private int seed;
	[SerializeField] private BiomeAttributes biomeAttributes;

	[field: Header("Player Data")]
	[field: SerializeField] public Transform Player { get; private set; }
	[SerializeField] private Vector3 spawnPosition;

	[field: Header("Blocks Data")]
	[field: SerializeField] public Material Material { get; private set; }
	[field: SerializeField] public Material TransparentMaterial { get; private set; }
	[field: SerializeField] public BlockType[] BlockTypes { get; private set; }

	private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

	private List<ChunkCoord> activeChunks = new List<ChunkCoord>();
	public ChunkCoord PlayerChunkCoord;
	private ChunkCoord playerLastChunkCoord;

	private List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
	private List<Chunk> chunksToUpdate = new List<Chunk>();

	private bool applyingModifications = false;

	private Queue<VoxelMod> modifications = new Queue<VoxelMod>();

	[Header("Debug")]
	[SerializeField] private GameObject debugScreen;

	private void Start() 
	{
		Random.InitState(seed);

		spawnPosition = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f, VoxelData.ChunkHeight - 50f, (VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f);
		GenerateWorld();
		playerLastChunkCoord = GetChunkCoordFromVector3(Player.position);
	}

	private void Update() 
	{
		PlayerChunkCoord = GetChunkCoordFromVector3(Player.position);

		if (!PlayerChunkCoord.Equals(playerLastChunkCoord))
			CheckViewDistance();

		if(modifications.Count > 0 && !applyingModifications)
		{
			StartCoroutine(ApplyModifications());
		}

		if(chunksToCreate.Count > 0)
		{
			CreateChunk();
		}

		if(chunksToUpdate.Count > 0)
		{
			UpdateChunks();
		}

		if (Input.GetKeyDown(KeyCode.F3))
			debugScreen.SetActive(!debugScreen.activeSelf);
	}

	private void GenerateWorld () 
	{
		for (int x = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; x < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; x++)
		{
			for (int z = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; z < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; z++)
			{
				chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, true);
				activeChunks.Add(new ChunkCoord(x, z));
			}
		}

		while(modifications.Count > 0)
		{
			VoxelMod v = modifications.Dequeue();
			ChunkCoord c = GetChunkCoordFromVector3(v.Position);

			if (chunks[c.x, c.z] == null)
			{
				chunks[c.x, c.z] = new Chunk(c, this, true);
				activeChunks.Add(c);
			}

			chunks[c.x, c.z].modifications.Enqueue(v);

			if (!chunksToUpdate.Contains(chunks[c.x, c.z]))
			{
				chunksToUpdate.Add(chunks[c.x, c.z]);
			}
		}

		for(int i = 0; i < chunksToUpdate.Count; i++)
		{
			chunksToUpdate[0].UpdateChunk();
			chunksToUpdate.RemoveAt(0);
		}

		Player.position = spawnPosition;
	}

	private void CreateChunk()
	{
		ChunkCoord c = chunksToCreate[0];
		chunksToCreate.RemoveAt(0);
		activeChunks.Add(c);
		chunks[c.x, c.z].Init();
	}

	private void UpdateChunks()
	{
		bool _update = false;
		int _index = 0;

		while (!_update && _index < chunksToUpdate.Count - 1)
		{
			if (chunksToUpdate[_index].IsVoxelMapPopulated)
			{
				chunksToUpdate[_index].UpdateChunk();
				chunksToUpdate.RemoveAt(_index);
				_update = true;
			}
			else
			{
				_index++;
			}
		}
	}

	private IEnumerator ApplyModifications()
	{
		applyingModifications = true;
		int _count = 0;

		while(modifications.Count > 0)
		{
			VoxelMod v = modifications.Dequeue();
			ChunkCoord c = GetChunkCoordFromVector3(v.Position);

			if (chunks[c.x, c.z] == null)
			{
				chunks[c.x, c.z] = new Chunk(c, this, true);
				activeChunks.Add(c);
			}

			chunks[c.x, c.z].modifications.Enqueue(v);

			if (!chunksToUpdate.Contains(chunks[c.x, c.z]))
			{
				chunksToUpdate.Add(chunks[c.x, c.z]);
			}

			_count++;

			if(_count > 200)
			{
				_count = 0;
				yield return null;
			}
		}

		applyingModifications = false;
	}

	private ChunkCoord GetChunkCoordFromVector3 (Vector3 pos) 
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
		return new ChunkCoord(x, z);
	}

	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
		return chunks[x, z];
	}

	private void CheckViewDistance () 
	{
		ChunkCoord coord = GetChunkCoordFromVector3(Player.position);
		playerLastChunkCoord = PlayerChunkCoord;

		List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

		for (int x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++) 
		{
			for (int z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++) 
			{
				if (IsChunkInWorld (new ChunkCoord (x, z))) 
				{
					if (chunks[x, z] == null) 
					{
						chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, false);
						chunksToCreate.Add(new ChunkCoord(x, z));
					} 
					else if (!chunks[x, z].isActive) 
					{
						chunks[x, z].isActive = true;
					}
					activeChunks.Add(new ChunkCoord(x, z));
				}

				for (int i = 0; i < previouslyActiveChunks.Count; i++) 
				{
					if (previouslyActiveChunks[i].Equals(new ChunkCoord(x, z)))
						previouslyActiveChunks.RemoveAt(i);
				}
			}
		}

		foreach (ChunkCoord c in previouslyActiveChunks)
			chunks[c.x, c.z].isActive = false;
	}

	public bool CheckForVoxel (Vector3 pos)
	{
		ChunkCoord thisChunk = new ChunkCoord(pos);

		if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight) return false;

		if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].IsVoxelMapPopulated)
		{
			return BlockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].IsSolid;
		}

		return BlockTypes[GetVoxel(pos)].IsSolid;
	}

	public bool CheckIfVoxelTransparent(Vector3 pos)
	{
		ChunkCoord thisChunk = new ChunkCoord(pos);

		if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight) return false;

		if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].IsVoxelMapPopulated)
		{
			return BlockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].IsTransparent;
		}

		return BlockTypes[GetVoxel(pos)].IsTransparent;
	}

	public byte GetVoxel (Vector3 pos) 
	{
		int yPos = Mathf.FloorToInt(pos.y);
		byte voxelValue = 2;
		int TerrainHeight = Mathf.FloorToInt(biomeAttributes.TerrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biomeAttributes.TerrainScale)) + biomeAttributes.SolidGroundHeight;

		/* IMMUTABLE PASS */

		// If outside world, return air.
		if (!IsVoxelInWorld(pos))
			return 0;

		// If bottom block of chunk, return bedrock.
		if (yPos == 0)
			return 1;

		/* BASIC TERRAIN PASS */

		if (voxelValue == 2)
		{
			foreach (Lode lode in biomeAttributes.Lodes)
			{
				if (yPos > lode.MinHeight && yPos < lode.MaxHeight)
					if (Noise.Get3DPerlin(pos, lode.NoiseOffset, lode.Scale, lode.Threshold))
						voxelValue = lode.BlockID;
			}
		}

		if (yPos == TerrainHeight && voxelValue != 0)
			voxelValue = 3;
		else if (yPos < TerrainHeight && yPos > TerrainHeight - 6 && voxelValue != 0)
			voxelValue = 4;
		else if (yPos > TerrainHeight)
			return 0;

		/* TREE PASS */

		if(yPos == TerrainHeight)
		{
			if(Noise.Get2DPerlin(new Vector2(pos.x, pos.y), 0, biomeAttributes.TreeZoneScale) > biomeAttributes.TreeZoneThreshold)
			{
				if(Noise.Get2DPerlin(new Vector2(pos.x, pos.y), 0, biomeAttributes.TreePlacementScale) > biomeAttributes.TreePlacementThreshold)
				{
					Structure.MakeTree(pos, modifications, biomeAttributes.MinTreeSize, biomeAttributes.MaxTreeSize);
				}
			}
		}

		return voxelValue;
	}

	private bool IsChunkInWorld (ChunkCoord coord)
	{
		if (coord.x > 0 && coord.x < VoxelData.WorldSizeInChunks - 1 && coord.z > 0 && coord.z < VoxelData.WorldSizeInChunks - 1)
			return true;
		else
			return
				false;
	}

	private bool IsVoxelInWorld (Vector3 pos) 
	{
		if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
			return true;
		else
			return false;
	}

}

[System.Serializable]
public class BlockType 
{
	[Header("Block Data")]
	public string BlockName;
	public bool IsSolid;
	public bool IsTransparent;
	public Sprite Icon;

	[Header("Texture Values")]
	public int BackFaceTexture;
	public int FrontFaceTexture;
	public int TopFaceTexture;
	public int BottomFaceTexture;
	public int LeftFaceTexture;
	public int RightFaceTexture;

	public int GetTextureID (int faceIndex) 
	{
		switch (faceIndex) 
		{
			case 0:
				return BackFaceTexture;
			case 1:
				return FrontFaceTexture;
			case 2:
				return TopFaceTexture;
			case 3:
				return BottomFaceTexture;
			case 4:
				return LeftFaceTexture;
			case 5:
				return RightFaceTexture;
			default:
				Debug.Log("Error in GetTextureID; invalid face index");
				return 0;
		}
	}
}

public class VoxelMod
{
	public Vector3 Position;
	public byte ID;

	public VoxelMod()
	{
		Position = new Vector3();
		ID = 0;
	}

	public VoxelMod(Vector3 _position, byte _id)
	{
		Position = _position;
		ID = _id;
	}
}