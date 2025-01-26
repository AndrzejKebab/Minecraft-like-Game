using Unity.Collections;

namespace PatataGames
{
	public struct Chunk
	{
		public NativeHashSet<ushort> BlockMap;
	}
}