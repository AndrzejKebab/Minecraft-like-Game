using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace _Project.WorldGeneration.TerraGen
{
	/// <summary>
	///     Port of RTF's tile filters: droplet erosion (tile/filter/Erosion.java) and
	///     smoothing (tile/filter/Smoothing.java), including the Modifier protection
	///     (no erosion below the waterline ramp, damped on badlands and near rivers;
	///     river channels are fully masked like RTF's cell.erosionMask).
	///     Runs over a tile's bordered TerraGenCell grid inside the tile generation
	///     job. Droplets are seeded per world-space 16-block cell so results are
	///     deterministic; tiles only diverge near their borders (same trade-off RTF
	///     makes with its tile border).
	/// </summary>
	[BurstCompile]
	public static class TerraErosion
	{
		private const int SEED_OFFSET  = 12_768; // RTF Erosion.Factory
		private const int BRUSH_RADIUS = 4;

		// ── droplet erosion ──────────────────────────────────────────────────

		public static unsafe void ApplyErosion(ref NativeArray<TerraGenCell> cells, int size,
		                                       int originBlockX, int originBlockZ,
		                                       in TerraGenSettings s, in TerraLevels levels)
		{
			if (!s.ErosionEnabled || s.ErosionDropletsPerChunk <= 0) return;

			// precompute the RTF erosion brush: disk of radius 4, weight 1 - d/r
			var brushDx = stackalloc int[64];
			var brushDz = stackalloc int[64];
			var brushW  = stackalloc float[64];
			var brushCount = 0;
			var brushSum   = 0f;
			for (var dz = -BRUSH_RADIUS; dz <= BRUSH_RADIUS; dz++)
			for (var dx = -BRUSH_RADIUS; dx <= BRUSH_RADIUS; dx++)
			{
				float sqrDst = dx * dx + dz * dz;
				if (sqrDst >= BRUSH_RADIUS * BRUSH_RADIUS) continue;
				var w = 1f - math.sqrt(sqrDst) / BRUSH_RADIUS;
				brushDx[brushCount] = dx;
				brushDz[brushCount] = dz;
				brushW[brushCount]  = w;
				brushSum           += w;
				brushCount++;
			}

			for (var i = 0; i < brushCount; i++) brushW[i] /= brushSum;

			var seed    = s.Seed + SEED_OFFSET;
			var cells16 = size >> 4; // droplets seeded per 16-block cell (RTF chunks)
			var maxPos  = size - 2f;

			for (var iteration = 0; iteration < s.ErosionDropletsPerChunk; iteration++)
			for (var cz = 0; cz < cells16; cz++)
			for (var cx = 0; cx < cells16; cx++)
			{
				// deterministic world-space seeding (RTF chunkSeed + iterationSeed)
				var seedX = (originBlockX >> 4) + cx;
				var seedZ = (originBlockZ >> 4) + cz;
				var rng   = DropletSeed(seed, seedX, seedZ, iteration);

				var px = math.clamp((cx << 4) + NextInt16(ref rng), 1f, maxPos);
				var pz = math.clamp((cz << 4) + NextInt16(ref rng), 1f, maxPos);

				ApplyDrop(ref cells, size, px, pz, brushDx, brushDz, brushW, brushCount,
				          in s, in levels);
			}
		}

		private static unsafe void ApplyDrop(ref NativeArray<TerraGenCell> cells, int size,
		                                     float posX, float posY,
		                                     int* brushDx, int* brushDz, float* brushW, int brushCount,
		                                     in TerraGenSettings s, in TerraLevels levels)
		{
			var dirX     = 0f;
			var dirY     = 0f;
			var sediment = 0f;
			var speed    = s.ErosionDropletVelocity;
			var water    = s.ErosionDropletVolume;

			for (var lifetime = 0; lifetime < s.ErosionDropletLifetime; lifetime++)
			{
				var nodeX        = (int)posX;
				var nodeY        = (int)posY;
				var dropletIndex = nodeY * size + nodeX;
				var cellOffsetX  = posX - nodeX;
				var cellOffsetY  = posY - nodeY;

				GradientAt(in cells, size, posX, posY,
				           out var height, out var gradientX, out var gradientY);

				dirX = dirX * 0.05f - gradientX * 0.95f;
				dirY = dirY * 0.05f - gradientY * 0.95f;
				var len = math.sqrt(dirX * dirX + dirY * dirY);
				if (float.IsNaN(len)) len = 0f;
				if (len != 0f)
				{
					dirX /= len;
					dirY /= len;
				}

				posX += dirX;
				posY += dirY;
				if ((dirX == 0f && dirY == 0f) ||
				    posX < 0f || posX >= size - 1 || posY < 0f || posY >= size - 1)
					return;

				GradientAt(in cells, size, posX, posY, out var newHeight, out _, out _);
				var deltaHeight = newHeight - height;

				var sedimentCapacity = math.max(-deltaHeight * speed * water * 4f, 0.01f);
				if (sediment > sedimentCapacity || deltaHeight > 0f)
				{
					var amountToDeposit = deltaHeight > 0f
						? math.min(deltaHeight, sediment)
						: (sediment - sedimentCapacity) * s.ErosionDepositRate;
					sediment -= amountToDeposit;

					Deposit(ref cells, dropletIndex,
					        amountToDeposit * (1f - cellOffsetX) * (1f - cellOffsetY), in levels);
					Deposit(ref cells, dropletIndex + 1,
					        amountToDeposit * cellOffsetX * (1f - cellOffsetY), in levels);
					Deposit(ref cells, dropletIndex + size,
					        amountToDeposit * (1f - cellOffsetX) * cellOffsetY, in levels);
					Deposit(ref cells, dropletIndex + size + 1,
					        amountToDeposit * cellOffsetX * cellOffsetY, in levels);
				}
				else
				{
					var amountToErode = math.min((sedimentCapacity - sediment) * s.ErosionRate,
					                             -deltaHeight);
					for (var i = 0; i < brushCount; i++)
					{
						var bx = nodeX + brushDx[i];
						var by = nodeY + brushDz[i];
						if (bx < 0 || bx >= size || by < 0 || by >= size) continue;

						var nodeIndex          = by * size + bx;
						var weighedErodeAmount = amountToErode * brushW[i];
						var deltaSediment      = math.min(cells[nodeIndex].Height, weighedErodeAmount);
						Erode(ref cells, nodeIndex, deltaSediment, in levels);
						sediment += deltaSediment;
					}
				}

				speed = math.sqrt(speed * speed + deltaHeight * 3f);
				water *= 0.99f;
				if (float.IsNaN(speed)) speed = 0f;
			}
		}

		/// <summary> Bilinear height + gradient at a sub-cell position (RTF TerrainPos.at). </summary>
		private static void GradientAt(in NativeArray<TerraGenCell> cells, int size,
		                               float posX, float posY,
		                               out float height, out float gradientX, out float gradientY)
		{
			var coordX = (int)posX;
			var coordY = (int)posY;
			var x      = posX - coordX;
			var y      = posY - coordY;
			var nw     = coordY * size + coordX;
			var heightNW = cells[nw].Height;
			var heightNE = cells[nw + 1].Height;
			var heightSW = cells[nw + size].Height;
			var heightSE = cells[nw + size + 1].Height;
			gradientX = (heightNE - heightNW) * (1f - y) + (heightSE - heightSW) * y;
			gradientY = (heightSW - heightNW) * (1f - x) + (heightSE - heightNE) * x;
			height = heightNW * (1f - x) * (1f - y) + heightNE * x * (1f - y)
			         + heightSW * (1f - x) * y + heightSE * x * y;
		}

		private static void Deposit(ref NativeArray<TerraGenCell> cells, int index, float amount,
		                            in TerraLevels levels)
		{
			TerraGenCell cell = cells[index];
			if (cell.Terrain == TerraTerrain.River) return; // erosionMask
			cell.Height  += Modify(in cell, amount, in levels);
			cells[index]  = cell;
		}

		private static void Erode(ref NativeArray<TerraGenCell> cells, int index, float amount,
		                          in TerraLevels levels)
		{
			TerraGenCell cell = cells[index];
			if (cell.Terrain == TerraTerrain.River) return; // erosionMask
			cell.Height  -= Modify(in cell, amount, in levels);
			cells[index]  = cell;
		}

		/// <summary>
		///     RTF Modifier.modify with Modifier.range(ground, ground + 15 blocks):
		///     no change below the waterline, ramping to full strength 15 blocks up;
		///     damped on badlands (erosionModifier 0.3) and near river valleys.
		/// </summary>
		private static float Modify(in TerraGenCell cell, float amount, in TerraLevels levels)
		{
			var min = levels.Ground;
			var max = levels.Ground + levels.Scale(15);
			float valueModifier;
			if (cell.Height > max) valueModifier = 1f;
			else if (cell.Height < min) valueModifier = 0f;
			else valueModifier = (cell.Height - min) / (max - min);

			var strengthModifier = 1f;
			if (cell.Terrain == TerraTerrain.Badlands)
			{
				var alpha = TerraNoise.Map(cell.RegionEdge, 0f, 0.15f, 0.15f);
				strengthModifier = math.lerp(1f, 0.3f, alpha);
			}

			if (cell.RiverMask < 0.1f)
				strengthModifier *= TerraNoise.Map(cell.RiverMask, 0.002f, 0.1f, 0.098f);

			return valueModifier * strengthModifier * amount;
		}

		// ── smoothing (tile/filter/Smoothing.java) ───────────────────────────

		public static void ApplySmoothing(ref NativeArray<TerraGenCell> cells, int size,
		                                  in TerraGenSettings s, in TerraLevels levels)
		{
			if (s.SmoothingIterations <= 0 || s.SmoothingRate <= 0f) return;

			var radius   = TerraNoise.Round(s.SmoothingRadius + 0.5f);
			var radiusSq = s.SmoothingRadius * s.SmoothingRadius;

			for (var iteration = 0; iteration < s.SmoothingIterations; iteration++)
			for (var z = radius; z < size - radius; z++)
			for (var x = radius; x < size - radius; x++)
			{
				var index = z * size + x;
				TerraGenCell cell = cells[index];
				if (cell.Terrain == TerraTerrain.River) continue; // erosionMask

				var total   = 0f;
				var weights = 0f;
				for (var dz = -radius; dz <= radius; dz++)
				for (var dx = -radius; dx <= radius; dx++)
				{
					float dist2 = dx * dx + dz * dz;
					if (dist2 > radiusSq) continue;
					var weight = 1f - dist2 / radiusSq;
					total   += cells[(z + dz) * size + (x + dx)].Height * weight;
					weights += weight;
				}

				if (weights <= 0f) continue;
				var dif = cell.Height - total / weights;

				// Modifier.range(ground+1, ground+120).invert(): smooth lowlands,
				// keep mountain detail
				var min = levels.Ground + levels.Scale(1);
				var max = levels.Ground + levels.Scale(120);
				float valueModifier;
				if (cell.Height > max) valueModifier = 0f;
				else if (cell.Height < min) valueModifier = 1f;
				else valueModifier = 1f - (cell.Height - min) / (max - min);

				var strengthModifier = 1f;
				if (cell.RiverMask < 0.1f)
					strengthModifier *= TerraNoise.Map(cell.RiverMask, 0.002f, 0.1f, 0.098f);

				cell.Height  -= valueModifier * strengthModifier * dif * s.SmoothingRate;
				cells[index]  = cell;
			}
		}

		// ── deterministic droplet RNG ────────────────────────────────────────

		private static uint DropletSeed(int seed, int seedX, int seedZ, int iteration)
		{
			var h = (uint)seed;
			h = Scramble(h ^ (uint)(seedX * 1_619));
			h = Scramble(h ^ (uint)(seedZ * 31_337));
			h = Scramble(h ^ (uint)(iteration * 6_971));
			return h;
		}

		private static uint Scramble(uint v)
		{
			v ^= v >> 16;
			v *= 0x7FEB352Du;
			v ^= v >> 15;
			v *= 0x846CA68Bu;
			v ^= v >> 16;
			return v;
		}

		private static int NextInt16(ref uint rng)
		{
			rng = Scramble(rng + 0x9E3779B9u);
			return (int)(rng & 15);
		}
	}
}
