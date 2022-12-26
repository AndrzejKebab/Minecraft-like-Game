using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelData
{
	public static readonly int ChunkWidth = 25;
	public static readonly int ChunkHeight = 500;
	public static readonly int WorldSizeInChunks = 10;

	public static int WorldSizeInVoxels 
	{
		get { return WorldSizeInChunks * ChunkWidth; }
	}

	public static readonly int ViewDistanceInChunks = 10;

	public static readonly int TextureAtlasSizeInBlocks = 4;
	public static float NormalizedBlockTextureSize { get { return 1f / (float)TextureAtlasSizeInBlocks; } }

	public static readonly Vector3[] VoxelVerticles = new Vector3[8]
	{
		new Vector3(0, 0, 0), //0
		new Vector3(1, 0, 0), //1
		new Vector3(1, 1, 0), //2
		new Vector3(0, 1, 0), //3
		new Vector3(0, 0, 1), //4
		new Vector3(1, 0, 1), //5
		new Vector3(1, 1, 1), //6
		new Vector3(0, 1, 1)  //7
	};

	public static readonly int[,] VoxelTriangles = new int[6, 4]
	{
		{0, 3, 1, 2}, //Back Face
		{5, 6, 4, 7}, //Front Face
		{3, 7, 2, 6}, //Top Face
		{1, 5, 0, 4}, //Bottom Face
		{4, 7, 0, 3}, //Left Face
		{1, 2, 5, 6}  //Right Face
	};

	public static readonly Vector2[] VoxelUvs = new Vector2[4]
	{
		new Vector2(0, 0),
		new Vector2(0, 1),
		new Vector2(1, 0),
		new Vector2(1, 1)
	};

	public static readonly Vector3[] FaceChecks = new Vector3[6]
	{
		new Vector3(0, 0, -1), //Back Face
		new Vector3(0, 0, 1),  //Front Face
		new Vector3(0, 1, 0),  //Top Face
		new Vector3(0, -1, 0), //Bottom Face
		new Vector3(-1, 0, 0), //Left Face
		new Vector3(1, 0, 0),  //Right Face
	};
}
