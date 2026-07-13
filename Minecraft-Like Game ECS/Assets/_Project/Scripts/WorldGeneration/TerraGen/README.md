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
| `Rivermap` / `RiverGenerator` / `Network` / `UpliftRiverCarver` | `TerraRiverNet` (branching networks) |
| voronoi-drainage fallback rivers            | `TerraRivers` (see below) |
| `Heightmap`                                 | `TerraHeightmap` |
| `TileGenerator` / `TileCache`               | `TerraTileGenJob` + `TerraTileSystem` |
| `tile/filter/Erosion` + `Smoothing` + `Modifier` | `TerraErosion` |
| `SpawnFinderFix`                            | `TerraSpawnSearchJob` + `PlayerSpawnSystem` |
| `Preset` settings                           | `TerraGenSettings` (singleton IComponentData) |

## Deliberate deviations from RTF

- **Rivers** — RTF builds explicit per-continent river networks; `TerraRiverNet`
  ports that architecture to Burst (`TerraRiverSeg` reaches, deterministic
  `TerraRng`, meander warp, carve zones, flow to sea). Two deviations from RTF:
  (1) RTF routes rivers with its uplift / water-table field; we approximate that
  with a **greedy downhill walk** from an upland source to the coast (steepest
  descent + coastward bias), emitting short reaches — so channels follow real
  valleys, join, and reach the sea without gouging canyons through ridges. Where
  a reach would climb a ridge it fades out (`MAX_CLIMB`) and resumes past it.
  (2) True confluences aren't snapped — tributaries run their own downhill valleys
  rather than merging into a parent channel. Water surfaces are monotonic
  (tracked from terrain), quantised into flat pools, and the tile `SettleWater`
  pass still guarantees no hanging water. A voronoi-drainage fallback
  (`TerraRivers`, `UseRiverNetworks = false`) remains for cheap rivers. No
  lakes/wetlands yet.

  Perf note: networks are rebuilt per tile for the continents it overlaps (a few
  thousand terrain samples per continent, Burst-compiled). A continent-keyed
  network cache is the obvious follow-up if tile generation hitches.
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
