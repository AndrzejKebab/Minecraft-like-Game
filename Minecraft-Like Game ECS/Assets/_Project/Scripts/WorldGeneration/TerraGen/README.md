# TerraGen — ReTerraForged-style world generation for DOTS

A Burst/Jobs port of [ReTerraForged](https://github.com/AndrzejKebab/ReTerraForged)'s
cell-based world generation pipeline. Columns are generated **once per tile** by
`TerraTileGenJob`, filtered (droplet erosion + smoothing, like RTF's WorldFilters),
cached in `TerraTileCacheSingleton`, and consumed by `ChunkPopulateJob` — so the N
vertical chunks of a column and all 16 chunk columns of a tile share one generation.
Everything is Burst-compiled and deterministic in world space.

## Pipeline (per column)

```
TerraHeightmap.Sample(x, z)
 ├─ TerraContinent.Apply      voronoi tectonic plates → continentEdge/id/distance
 ├─ TerraTerrains.ApplyRegion voronoi terrain provinces → regionId/edge/centre
 ├─ land  = Blender(mountainShape,
 │            RegionLerper(border, RegionSelector(steppe|plains|dales|hills|
 │                        torridonian|plateau|badlands|mountains1-3)),
 │            mountainChain)
 ├─ ocean = ContinentLerper3(deepOcean, shallowOcean, coast)
 ├─ height = ContinentLerper2(ocean, land)  by continentEdge
 ├─ TerraRivers.Apply         valley → banks → bed carve, river water level
 └─ TerraClimate.Apply        biome voronoi + latitude temperature + moisture
                              → Whittaker biome (+ mountain/coast overrides)
```

## Tile cache (port of RTF TileGenerator/TileCache)

- A tile = 4×4 chunk columns (128×128 blocks) generated with a 32-block border
  (`TerraTileConst`), so filters have context and every chunk can read a full
  1-chunk halo for decoration without touching neighbouring tiles.
- `TerraTileSystem` (runs before `ChunkPopulateSystem`) schedules `TerraTileGenJob`
  for tiles needed by chunks awaiting population — nearest first, budgeted per
  frame — and evicts tiles outside the keep radius. The map is main-thread-only;
  populate jobs get per-batch `TerraTileSlice` views and register themselves on
  the tile's `ReadHandle`, so a tile is never disposed under a running job.
- `TerraTileGenJob` samples the pipeline over the bordered grid, then applies the
  RTF filters in order: **droplet erosion** (`TerraErosion.ApplyErosion`, port of
  `tile/filter/Erosion.java`: 135 droplets per 16-block cell, lifetime 12, brush
  radius 4, Modifier protection for coasts/badlands/river valleys) and
  **smoothing** (`ApplySmoothing`, port of `Smoothing.java`: radius 1.8, rate 0.9,
  strongest in lowlands). River channels are erosion-masked like RTF's
  `cell.erosionMask`.
- `ChunkPopulateJob` reads its 32×32 columns from the slice, with fast paths:
  chunks fully above terrain+water are filled as air and tagged `IsEmpty`
  immediately; chunks fully below every surface skip per-voxel classification and
  fill stone before caves/ores run.

`TerraGenerator.ClassifyVoxel` turns a column into blocks (biome surfaces, rocky
peaks, sea/river water, beaches).

## Spawn on land

`PlayerSpawnSystem` replaces the scene-authored spawn position: at startup a
Burst job (`TerraSpawnSearchJob`) spirals outward from the origin until it finds
an inland land column (above sea, below the peaks, outside river valleys), then
the system pins the player above it — zeroed velocity every frame — while chunks
stream in, refines the exact surface Y from real block data once the ground
chunk is populated, and releases the player when its collider exists. Fully
deterministic per seed.

## RTF → C# mapping

| ReTerraForged (Java)                        | Here                          |
|---------------------------------------------|-------------------------------|
| `NoiseUtil`, `Perlin`, `PerlinRidge`, `Billow`, `WorleyEdge`, `Warp`, module `Erosion` | `TerraNoise` |
| `Cell`, `Levels`, `TerrainType`, `BiomeType` enum | `TerraCell` |
| `AdvancedContinentGenerator` + `AbstractContinent` | `TerraContinent` |
| `RegionModule`, `RegionSelector`, `RegionLerper`, `Populators`, `Blender`, `OceanPopulator` | `TerraTerrains` |
| `ClimateModule`, `Climate`, `BiomeType.getCurve` | `TerraClimate` |
| `Rivermap` / `RiverGenerator` / `RiverCarver` | `TerraRivers` (see below) |
| `Heightmap`                                 | `TerraHeightmap` |
| `TileGenerator` / `TileCache`               | `TerraTileGenJob` + `TerraTileSystem` |
| `tile/filter/Erosion` + `Smoothing` + `Modifier` | `TerraErosion` |
| `SpawnFinderFix`                            | `TerraSpawnSearchJob` + `PlayerSpawnSystem` |
| `Preset` settings                           | `TerraGenSettings` (singleton IComponentData) |

## Deliberate deviations from RTF

- **Rivers** — RTF builds explicit per-continent river networks (object graphs of
  line segments, cached per region). That doesn't translate to stateless Burst
  jobs, so rivers here follow the edges of a warped drainage voronoi instead:
  connected branching channels with an RTF-style valley→banks→bed profile carved
  in real block units through the hypsometric curve, and a local water level that
  follows the banks. They fade into the sea across the coast band and fade out
  above ~100 blocks of elevation (no network solver = no consistent water levels
  on steep slopes; mountain valleys come from the droplet erosion instead). Water
  levels can still step slightly along a channel. No lakes/wetlands yet.
- **CELL_2D table** — RTF's 256-entry jitter table is replaced by a procedural
  hash (Burst can't access managed static arrays). Same range and character, not
  bit-compatible with Java worlds.
- **Terrace/AdvancedTerrace/Steps/Boost/PowCurve** are behavioural approximations
  (plateau/badlands strata look, not exact curve math).
- **Tile seams** — droplet paths are clipped at each tile's bordered grid, so
  adjacent tiles can disagree slightly at their boundary (RTF has the same
  trade-off; it uses a 16-block border, ours is 32). Measured: cross-tile height
  steps average ~0.7 blocks vs ~0.6 within-tile — visually indistinguishable.
- **Biome table** — RTF loads a Whittaker diagram image; `TerraClimate.GetBiome`
  implements the equivalent temperature × humidity band grid.
- Mushroom islands, volcanoes, archipelagos, wetlands, lakes: not ported yet.

## Tuning

Everything lives in `TerraGenSettings` (defaults mirror RTF's preset). Key knobs,
exposed on the `WorldSettings` MonoBehaviour:

- `OceanDepth` (256) / `MountainHeight` (512) — vertical mapping. The pipeline
  runs in a fixed RTF-proportioned space; output heights go through a
  **hypsometric curve** (like real-world elevation distribution): most land is
  low and gentle, mountainsides steepen exponentially, typical big mountains hit
  ~`MountainHeight` and rare peaks reach ~2×. Sea level is ALWAYS world Y 0. The
  ocean has its own curve with a shallow shelf near shore, so inland dips become
  marshes instead of deep lakes.
- `ContinentScale` (3000) — plate size; ocean/land balance comes from the control
  points (`DeepOcean`..`Inland`).
- `TerrainRegionSize` (1200) — size of plains/hills/mountains provinces.
- `BiomeSize` (800) — climate region size; `TemperatureScale` sets latitude band
  width in biome cells.
- `RiversEnabled`, `RiverScale`, widths/depths — drainage density and channel size.
- `FancyMountains` — functional-erosion mountain detail (pricier, prettier).
- `ErosionEnabled` / `ErosionDropletsPerChunk` (135) and the other droplet
  parameters, `SmoothingIterations`/`Radius`/`Rate` — the per-tile filters.

FastNoise2 still drives 3D caves and ore placement (`CavesNoise`); the RTF
pipeline itself needs per-cell voronoi metadata (ids, edges, centres) that a
FastNoise2 node graph can't express, so it runs as Burst-compiled scalar code.
