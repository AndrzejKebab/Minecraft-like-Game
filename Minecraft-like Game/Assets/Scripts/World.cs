using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
	[field:SerializeField] public Material Material { get; private set; }
	[field:SerializeField] public BlockType[] BlockTypes { get; private set; }

	private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

	private void Start()
	{
		GenerateWorld();
	}

	private void GenerateWorld()
	{
		for(int x = 0; x < VoxelData.WorldSizeInChunks; x++)
		{
			for(int z = 0; z < VoxelData.WorldSizeInChunks; z++)
			{
				CreateNewChunk(x, z);
			}
		}
	}

	private void CreateNewChunk(int x, int z)
	{
		chunks[x, z] = new Chunk(new ChunkCoord(x, z), this);
	}
}

[System.Serializable]
public class BlockType
{
	[field:SerializeField] public string BlockName { get; private set; }
	[field:SerializeField] public bool isSolid { get; private set;}

	[Header("Texture Values")]
	[SerializeField] private int backFaceTextture;
	[SerializeField] private int frontFaceTextture;
	[SerializeField] private int topFaceTextture;
	[SerializeField] private int bottomFaceTextture;
	[SerializeField] private int leftFaceTextture;
	[SerializeField] private int rightFaceTextture;

	public int GetTextureID (int faceIndexID)
	{
		switch(faceIndexID)
		{
			case 0:
				return backFaceTextture;
			case 1:
				return frontFaceTextture;
			case 2:
				return topFaceTextture;
			case 3:
				return bottomFaceTextture;
			case 4:
				return leftFaceTextture;
			case 5:
				return rightFaceTextture;
			default:
				Debug.Log("Ding Dong something went wrong!");
				return 0;
		}
	}
}