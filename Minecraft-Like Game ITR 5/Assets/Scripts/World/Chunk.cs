using Unity.Collections;
using Unity.Entities;

namespace PatataStudio
{
	public struct Chunk
	{
		public NativeArray<ushort> Voxels;
	}
}