using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PatataGames
{
	public class ChunkSystem : MonoBehaviour
	{
		public  List<BlockType>            BlockTypeList;
		private NativeList<int>            nativeBlockSetList;
		public  List<MeshType>             MeshTypeList;
		private NativeList<NativeMeshData> nativeMeshList;

		public  NativeParallelHashMap<int3, Chunk> ChunkMap;
		private NativeHashSet<int3>                chunksToUpdate;
		private NativeHashSet<int3>                activeChunks;
	}
}