using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct ChunkJob : IJob
{
	public struct NativeMeshData
	{
		public NativeList<Vertex> Vertex;
		public NativeList<ushort> MeshTriangles;
	}

	public struct NativeChunkData
	{
		public NativeArray<BlockTypesJob> BlockTypes;
		public NativeArray<ushort>        VoxelMap;

		public NativeArray<ushort> NeighborZNeg;
		public NativeArray<ushort> NeighborZPos;
		public NativeArray<ushort> NeighborYPos;
		public NativeArray<ushort> NeighborYNeg;
		public NativeArray<ushort> NeighborXNeg;
		public NativeArray<ushort> NeighborXPos;

		[MarshalAs(UnmanagedType.U1)] public bool HasNeighborZNeg;
		[MarshalAs(UnmanagedType.U1)] public bool HasNeighborZPos;
		[MarshalAs(UnmanagedType.U1)] public bool HasNeighborYPos;
		[MarshalAs(UnmanagedType.U1)] public bool HasNeighborYNeg;
		[MarshalAs(UnmanagedType.U1)] public bool HasNeighborXNeg;
		[MarshalAs(UnmanagedType.U1)] public bool HasNeighborXPos;
	}

	[ReadOnly] public NativeChunkData ChunkData;
	public            NativeMeshData  MeshData;

	[ReadOnly] public int  ChunkSize;
	[ReadOnly] public int  WorldSizeInVoxels;
	[ReadOnly] public int3 Position;

	private ushort vertexIndex;

	public void Execute()
	{
		CreateMeshData();
	}

	private void CreateMeshData()
	{
		for (var y = 0; y < ChunkSize; y++)
		for (var x = 0; x < ChunkSize; x++)
		for (var z = 0; z < ChunkSize; z++)
			if (ChunkData.BlockTypes[ChunkData.VoxelMap.GetAtPosition(x, y, z)].IsSolid)
				AddVoxelDataToChunk(new int3(x, y, z));
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Packed vertex layout (one uint per vertex — see Vertex struct):
	//    bits  0– 5   pos.x      (0–32)
	//    bits  6–11   pos.y      (0–32)
	//    bits 12–17   pos.z      (0–32)
	//    bits 18–20   faceIndex  (0–5)   → normal + tangent via shader table
	//    bits 21–22   uvCorner   (0–3)   → float2(corner>>1, corner&1)
	//    bits 23–30   texIndex   (0–255) → Texture2DArray slice
	// ─────────────────────────────────────────────────────────────────────────
	private void AddVoxelDataToChunk(int3 pos)
	{
		for (var face = 0; face < 6; face++)
		{
			if (CheckVoxel(pos + VoxelData.FaceChecks[face])) continue;

			var blockID  = ChunkData.VoxelMap.GetAtPosition(pos.x, pos.y, pos.z);
			var texIndex = ChunkData.BlockTypes[blockID].GetTexture2D(face);

			MeshData.MeshTriangles.Add(vertexIndex);
			MeshData.MeshTriangles.Add((ushort)(vertexIndex + 1));
			MeshData.MeshTriangles.Add((ushort)(vertexIndex + 3));
			MeshData.MeshTriangles.Add(vertexIndex);
			MeshData.MeshTriangles.Add((ushort)(vertexIndex + 3));
			MeshData.MeshTriangles.Add((ushort)(vertexIndex + 2));

			for (byte corner = 0; corner < 4; corner++)
			{
				var  vIdx = VoxelData.VoxelTriangles[face * 4 + corner];
				var vx   = VoxelData.VoxelVertices[vIdx].x;
				var vy   = VoxelData.VoxelVertices[vIdx].y;
				var vz   = VoxelData.VoxelVertices[vIdx].z;

				MeshData.Vertex.Add(new Vertex(
				                               pos.x + vx,
				                               pos.y + vy,
				                               pos.z + vz,
				                               face, corner, texIndex));
			}

			vertexIndex += 4;
		}
	}

	private bool IsVoxelInChunk(int3 pos)
	{
		return pos.x >= 0 && pos.x <= ChunkSize - 1 &&
		       pos.y >= 0 && pos.y <= ChunkSize - 1 &&
		       pos.z >= 0 && pos.z <= ChunkSize - 1;
	}

	private bool CheckVoxel(int3 pos)
	{
		if (IsVoxelInChunk(pos))
			return ChunkData.BlockTypes[ChunkData.VoxelMap.GetAtPosition(pos.x, pos.y, pos.z)].IsSolid;

		if (pos.z < 0 && ChunkData.HasNeighborZNeg)
			return ChunkData.BlockTypes[ChunkData.NeighborZNeg.GetAtPosition(pos.x, pos.y, ChunkSize - 1)]
			                .IsSolid;
		if (pos.z >= ChunkSize && ChunkData.HasNeighborZPos)
			return ChunkData.BlockTypes[ChunkData.NeighborZPos.GetAtPosition(pos.x, pos.y, 0)].IsSolid;
		if (pos.y >= ChunkSize && ChunkData.HasNeighborYPos)
			return ChunkData.BlockTypes[ChunkData.NeighborYPos.GetAtPosition(pos.x, 0, pos.z)].IsSolid;
		if (pos.y < 0 && ChunkData.HasNeighborYNeg)
			return ChunkData.BlockTypes[ChunkData.NeighborYNeg.GetAtPosition(pos.x, ChunkSize - 1, pos.z)]
			                .IsSolid;
		if (pos.x < 0 && ChunkData.HasNeighborXNeg)
			return ChunkData.BlockTypes[ChunkData.NeighborXNeg.GetAtPosition(ChunkSize - 1, pos.y, pos.z)]
			                .IsSolid;
		if (pos.x >= ChunkSize && ChunkData.HasNeighborXPos)
			return ChunkData.BlockTypes[ChunkData.NeighborXPos.GetAtPosition(0, pos.y, pos.z)].IsSolid;

		return true;
	}
}