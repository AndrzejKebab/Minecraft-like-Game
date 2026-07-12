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

		public static void Sample(out TerraCell cell, float x, float z,
		                          in TerraGenSettings s, in TerraLevels levels)
		{
			cell         = TerraCell.Default();
			cell.Terrain = TerraTerrain.Flats;

			// beach noise: perlin2(20, 1) scaled to ~4 real blocks above the sea
			cell.BeachNoise = TerraNoise.Perlin(x, z, s.Seed + SEED_BEACH, 1f / 20f, 1)
			                  * levels.BlocksAboveSea(4f);

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

			TerraRivers.Apply(ref cell, x, z, in s, in levels);

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

			// beach detection: coast terrain sitting within beach-noise reach of the
			// waterline becomes actual beach (simplified BeachDetect filter)
			if (cell.Terrain == TerraTerrain.Coast &&
			    cell.Height <= levels.Ground + math.abs(cell.BeachNoise))
				cell.Terrain = TerraTerrain.Beach;
		}

		/// <summary>
		///     Land stack: RegionLerper(border, RegionSelector(terrains)) blended with
		///     mountain chains through the mountainShape control noise (RTF Blender).
		/// </summary>
		private static TerrainSample SampleLand(ref TerraCell cell, float tx, float tz,
		                                        float ground, in TerraGenSettings s)
		{
			const float blendLower = 0.3f;
			const float blendUpper = 0.8f;
			const float split      = 0.575f;

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
