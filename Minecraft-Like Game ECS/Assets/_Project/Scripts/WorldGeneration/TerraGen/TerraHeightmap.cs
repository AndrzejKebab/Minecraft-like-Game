using Unity.Burst;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Port of RTF Heightmap — the top-level cell pipeline:
	///     continent → terrain regions → populator blend (with mountain chains) →
	///     ocean/land continent lerp → rivers → climate.
	/// </summary>
	[BurstCompile]
	public static class TerraHeightmap
	{
		private const int SEED_BEACH = 900_000;

		/// <summary>
		///     Standalone per-column sample: terrain → voronoi rivers → climate.
		///     Used by the spawn finder and tooling. The tile generator instead runs
		///     SampleTerrain → TerraRiverNet carve → ApplyClimate so the branching
		///     networks (which need a pre-built segment list) shape the terrain.
		/// </summary>
		public static void Sample(out TerraCell cell, float x, float z,
		                          in TerraGenSettings s, in TerraLevels levels)
		{
			SampleTerrain(out cell, x, z, in s, in levels);
			TerraRivers.Apply(ref cell, x, z, in s, in levels);
			ApplyClimate(ref cell, x, z, in s, in levels);
		}

		/// <summary> Stage 1: continent → regions → land/ocean blend (no rivers/climate). </summary>
		public static void SampleTerrain(out TerraCell cell, float x, float z,
		                                 in TerraGenSettings s, in TerraLevels levels)
		{
			cell         = TerraCell.Default();
			cell.Terrain = TerraTerrain.Flats;

			// beach noise: raw perlin in ±3 blocks, ragged shoreline width
			cell.BeachNoise = (TerraNoise.Perlin(x, z, s.Seed + SEED_BEACH, 1f / 20f, 1) * 2f - 1f) * 3f;

			TerraContinent.Apply(ref cell, x, z, in s);

			var edge   = cell.ContinentEdge;
			var ground = levels.Ground;

			// terrain populators run at global horizontal scale
			var terrainFrequency = 1f / math.max(0.05f, s.GlobalHorizontalScale);
			var tx = x * terrainFrequency;
			var tz = z * terrainFrequency;

			TerrainSample result;
			if (edge < s.ShallowOcean)
			{
				// pure ocean — skip the land stack entirely
				result = SampleOceans(tx, tz, edge, in levels, in s);
			}
			else
			{
				TerraTerrains.ApplyRegion(ref cell, x, z, in s);
				TerrainSample land = SampleLand(ref cell, tx, tz, ground, in s);

				if (edge > s.Inland)
				{
					result = land;
				}
				else
				{
					// ContinentLerper2(oceans, land, shallowOcean, inland):
					// height lerps, categorical fields follow the land side
					TerrainSample oceans = SampleOceans(tx, tz, edge, in levels, in s);
					var alpha = (edge - s.ShallowOcean) / (s.Inland - s.ShallowOcean);
					result        = land;
					result.Height = math.lerp(oceans.Height, land.Height, alpha);
				}
			}

			cell.Height    = result.Height;
			cell.Erosion   = result.Erosion;
			cell.Weirdness = result.Weirdness;
			cell.Terrain   = result.Terrain;
		}

		/// <summary> Terrain height in blocks (river water-level assignment). </summary>
		public static float SampleLandHeightBlocks(float x, float z,
		                                           in TerraGenSettings s, in TerraLevels levels)
		{
			SampleTerrain(out TerraCell cell, x, z, in s, in levels);
			return levels.ToBlocksF(cell.Height);
		}

		/// <summary> Stage 2: river-valley climate tweaks + climate + beach detection. </summary>
		public static void ApplyClimate(ref TerraCell cell, float x, float z,
		                                in TerraGenSettings s, in TerraLevels levels)
		{
			// river-valley climate tweaks (Heightmap.applyClimate)
			if (cell.RiverMask < 0.675f)
			{
				cell.Erosion   = 0.445f;
				cell.Weirdness = 0.34f;
			}

			if (cell.Terrain == TerraTerrain.River)
			{
				cell.Erosion   = -0.05f;
				cell.Weirdness = -0.03f;
			}

			TerraClimate.Apply(ref cell, x, z, in s, in levels);

			// beach detection: land right at the OCEAN shore becomes sand — block
			// based (so the very waterline is sand, not a grass line) and gated to
			// the coastal band so inland river valleys keep grass banks. Mountain
			// skirts reaching the sea are included (RTF's coast-override skips them).
			if (cell.Terrain != TerraTerrain.River && !cell.Terrain.IsSubmerged() &&
			    cell.ContinentEdge < s.Coast + 0.07f)
			{
				var surfY      = levels.ToBlockY(cell.Height);
				var beachTopY  = 3 + (int)math.round(math.abs(cell.BeachNoise));
				if (surfY >= 0 && surfY <= beachTopY)
					cell.Terrain = TerraTerrain.Beach;
			}
		}

		/// <summary>
		///     Land stack: RegionLerper(border, RegionSelector(terrains)) blended with
		///     mountain chains through the mountainShape control noise (RTF Blender).
		/// </summary>
		private static TerrainSample SampleLand(ref TerraCell cell, float tx, float tz,
		                                        float ground, in TerraGenSettings s)
		{
			// wider blend than RTF (0.3..0.8) — with 512-block peaks the mountains
			// need broad foothill skirts or they read as spikes. High blendUpper so
			// only the strongest shape becomes a full mountain chain (fewer mountains)
			const float blendLower = 0.35f;
			const float blendUpper = 0.92f;
			const float split      = 0.7f;

			var shape = TerraTerrains.MountainShape(tx, tz, in s);

			// evaluate only the sides the blend actually needs
			if (shape > blendUpper)
				return TerraTerrains.MountainChain(tx, tz, ground, in s);

			TerrainSample region = TerraTerrains.SelectRegionTerrain(cell.TerrainRegionId, tx, tz, ground, in s);
			TerrainSample border = TerraTerrains.Border(tx, tz, ground, in s);
			TerrainSample blend  = TerraTerrains.LerpRegion(in border, in region, cell.TerrainRegionEdge);

			if (shape < blendLower)
				return blend;

			TerrainSample chain = TerraTerrains.MountainChain(tx, tz, ground, in s);
			return TerraTerrains.BlendTerrain(shape, in blend, in chain, blendLower, blendUpper, split);
		}

		/// <summary> ContinentLerper3(deepOcean, shallowOcean, coast) with CURVE3 easing. </summary>
		private static TerrainSample SampleOceans(float tx, float tz, float edge,
		                                          in TerraLevels levels, in TerraGenSettings s)
		{
			var min = s.DeepOcean;
			var mid = s.ShallowOcean;
			var max = s.Coast;

			if (edge < min)
				return TerraTerrains.DeepOcean(tx, tz, in levels, in s);
			if (edge > max)
				return TerraTerrains.Coast(in levels);

			if (edge < mid)
			{
				var alpha = TerraNoise.InterpHermite((edge - min) / (mid - min));
				TerrainSample deep    = TerraTerrains.DeepOcean(tx, tz, in levels, in s);
				TerrainSample shallow = TerraTerrains.ShallowOcean(in levels);
				var result = shallow;
				result.Height = math.lerp(deep.Height, shallow.Height, alpha);
				return result;
			}
			else
			{
				var alpha = TerraNoise.InterpHermite((edge - mid) / (max - mid));
				TerrainSample shallow = TerraTerrains.ShallowOcean(in levels);
				TerrainSample coast   = TerraTerrains.Coast(in levels);
				var result = coast;
				result.Height = math.lerp(shallow.Height, coast.Height, alpha);
				return result;
			}
		}
	}
}
