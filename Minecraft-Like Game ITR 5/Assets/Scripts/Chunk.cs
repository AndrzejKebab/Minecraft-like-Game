using Unity.Collections;

namespace PatataStudio
{
	public struct Chunk
	{
		public NativeArray<bool> voxels; // solid = true, air = false
	}
}