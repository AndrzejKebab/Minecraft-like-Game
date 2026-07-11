# TerraGen — ReTerraForged-style world generation for DOTS

A Burst/Jobs port of [ReTerraForged](https://github.com/AndrzejKebab/ReTerraForged)'s
cell-based world generation pipeline. Everything runs inside `ChunkPopulateJob`
(IJobParallelFor, Burst-compiled) as pure functions of `(x, z, seed)` — no managed
state, no caches, fully deterministic per chunk.

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

`TerraGenerator.GenerateHaloColumns` fills the 3×3-chunk halo of `TerraColumn`s
(surfaceY, waterY, biome, terrain, riverMask) and `ClassifyVoxel` turns a column
into blocks (biome surfaces, rocky peaks, sea/river water, beaches).

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
| `Preset` settings                           | `TerraGenSettings` (singleton IComponentData) |

## Deliberate deviations from RTF

- **Rivers** — RTF builds explicit per-continent river networks (object graphs of
  line segments, cached per region). That doesn't translate to stateless Burst
  jobs, so rivers here follow the edges of a warped drainage voronoi instead:
  connected branching channels with an RTF-style valley→banks→bed profile and a
  local water level, fading into the sea across the coast band. No lakes/wetlands
  yet.
- **CELL_2D table** — RTF's 256-entry jitter table is replaced by a procedural
  hash (Burst can't access managed static arrays). Same range and character, not
  bit-compatible with Java worlds.
- **Terrace/AdvancedTerrace/Steps/Boost/PowCurve** are behavioural approximations
  (plateau/badlands strata look, not exact curve math).
- **Droplet erosion filter** (RTF `tile/filter/Erosion`) is *not* ported: it needs
  a shared tile cache to avoid chunk seams. The functional erosion noise
  (`fancy mountains`) *is* ported and enabled by default. If you later add a
  heightmap-tile cache system, the droplet filter is the natural next step.
- **Biome table** — RTF loads a Whittaker diagram image; `TerraClimate.GetBiome`
  implements the equivalent temperature × humidity band grid.
- Mushroom islands, volcanoes, archipelagos, wetlands, lakes: not ported yet.

## Tuning

Everything lives in `TerraGenSettings` (defaults mirror RTF's preset). Key knobs,
exposed on the `WorldSettings` MonoBehaviour:

- `WorldHeight` / `SeaLevel` — vertical mapping. Normalised height × WorldHeight,
  then shifted so `SeaLevel` sits at world Y 0 (the game's water line).
- `ContinentScale` (3000) — plate size; ocean/land balance comes from the control
  points (`DeepOcean`..`Inland`).
- `TerrainRegionSize` (1200) — size of plains/hills/mountains provinces.
- `BiomeSize` (800) — climate region size; `TemperatureScale` sets latitude band
  width in biome cells.
- `RiversEnabled`, `RiverScale`, widths/depths — drainage density and channel size.
- `FancyMountains` — functional-erosion mountain detail (pricier, prettier).

FastNoise2 still drives 3D caves and ore placement (`CavesNoise`); the RTF
pipeline itself needs per-cell voronoi metadata (ids, edges, centres) that a
FastNoise2 node graph can't express, so it runs as Burst-compiled scalar code.
