namespace _Project.WorldGeneration.Blocks
{
	public enum BlockDirectionType : byte
	{
		None    = 0, // Standard blocks (Dirt, Stone)
		YAxis   = 1, // Rotates around Y-axis to face the player horizontally (Furnace, Pumpkin)
		AllAxes = 2  // Rotates to face the surface normal it was placed against (Logs, Basalt)
	}
}