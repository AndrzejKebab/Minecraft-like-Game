# CLAUDE.md

Guidance for Claude Code (claude.ai/code) working in this repo.

## Project Overview

Minecraft-style voxel game on Unity's **DOTS stack** (ECS + Burst + Job System + Unity Physics). All perf-critical code runs as Burst-compiled jobs over NativeContainers — no MonoBehaviour logic in hot paths.

Unity version: confirmed via ProjectSettings. Packages via `Packages/manifest.json`.

## Build & Development

No CLI build scripts. Dev happens in Unity Editor only:
- Open `Minecraft-Like Game ECS.sln` in Rider or Visual Studio for IDE support.
- Enter Play Mode to run.
- Burst compiles automatically; verify via **Jobs → Burst → Open Inspector**.
- Debug ECS state via **Window → Analysis → Profiler** and **Entity Debugger** (`Window → Entities → Hierarchy`).

## Architecture

### Chunk Pipeline (Core Data Flow)

Each frame, chunks flow strict dependency chain:

```
PlayerVisibleChunksSystem     [SimulationSystemGroup, UpdateFirst]
        ↓ Creates chunk entities, manages ChunkMapSingleton
ChunkPopulateSystem           [SimulationSystemGroup]
        ↓ TerrainShapePassJob → CavesPassJob → DecorationPassJob
        ↓ Adds IsPopulated tag when all three stages complete
ChunkMeshBuilderSystem        [SimulationSystemGroup, After PopulateSystem]
        ↓ GreedyMeshJob → writes NativeMesh (solid + fluid)
ChunkCollidersSystem          [FixedStepSimulationSystemGroup, OrderFirst]
        ↓ Bakes Unity Physics colliders async (near-player only)
ChunkRenderSystem             [PresentationSystemGroup, OrderFirst]
        ↓ Frustum cull → GPU indirect draw via GraphicsBuffer
ChunkManagerSystem            [SimulationSystemGroup, OrderLast]
        ↓ Cleanup: disposes NativeArrays, destroys out-of-range entities
```

### Key Singletons

| Singleton | Purpose |
|---|---|
| `ChunkMapSingleton` | `NativeHashMap<int3, Entity>` — ground truth for loaded chunks |
| `WorldSettingsSingleton` | Seed, FastNoise2 node tree, biome curves (`NativeCurve`) |
| `WorldBlockRegistrySingleton` | All block prototypes, mesh data, ore/tree generation rules |

### Chunk Coordinate System

- Chunk size: **32³ blocks** (`VoxelData.CHUNK_SIZE = 32`)
- `ChunkCoord` (int3) → `WorldPosition = ChunkCoord * 32`
- View distance: **8 chunks** radius (`GameSettings.ViewDistanceInChunks`)
- Max concurrent terrain gen jobs: **1** (`GameSettings.MAX_CONCURRENT_JOBS`)

### Per-Chunk Components

```
ChunkPositionComponent   → ChunkCoord (int3), WorldPosition (float3)
ChunkComponent           → BlockData (NativeArray<BlockState>, 32³)
ChunkMeshData            → SolidMesh + FluidMesh (NativeMesh)
ChunkGfxBuffers          → VertexBuffer, IndexBuffer, ArgsBuffer (GraphicsBuffers)
```

`BlockState` is 4 bytes: `ID (ushort)` + `Orientation (byte)`.

### Terrain Generation (ChunkPopulateSystem)

Three sequential Burst jobs per chunk:
1. **TerrainShapePassJob** — FastNoise2 heightmap → classifies each voxel (air / stone / dirt / grass)
2. **CavesPassJob** — carves cave volumes
3. **DecorationPassJob** — places trees and ores; uses **4-color checkerboard wave** so adjacent chunks decorate in parallel without write conflicts

### Greedy Meshing (GreedyMeshJob)

Merges coplanar same-block faces into larger quads (~60-70% triangle reduction vs. naive cube meshing). Requires neighbor chunk `BlockData` for correct face culling at borders — mesh jobs depend on all 6 neighbors populated first.

### Rendering (ChunkRenderSystem)

- **Frustum culling** per chunk before draw calls
- **GPU indirect draw** via `GraphicsBuffer` (StructuredBuffer in shader) — chunk origin + vertex buffer passed to shader, no per-vertex CPU upload per frame
- Solid and fluid meshes use separate materials/passes

### Character Systems

- `FirstPersonCharacterPhysicsUpdateSystem` — kinematic controller via Unity.CharacterController
- `PlayerInteractionSystem` — block break/place raycast
- `MainCameraSystem` — follows player entity

### Bootstrap

`GameBootstrap.cs` runs on start:
- Loads block definitions from `WorldBlockRegistrySingleton`
- Generates texture arrays (`TextureArrayGenerator`) for block atlas
- Sets up `WorldSettingsSingleton` with noise config and biome curves

## Key Files

| File | Role |
|---|---|
| `Assets/_Project/Scripts/GameBootstrap.cs` | Entry point, registry setup |
| `Assets/_Project/Scripts/GameSettings.cs` | Global constants (view distance, chunk size, job limits) |
| `Assets/_Project/Scripts/VoxelData.cs` | Chunk size, face directions, vertex normals |
| `Assets/_Project/Scripts/WorldGeneration/Systems/PlayerVisibleChunksSystem.cs` | Chunk load/unload |
| `Assets/_Project/Scripts/WorldGeneration/Systems/ChunkPopulateSystem.cs` | Terrain job orchestration |
| `Assets/_Project/Scripts/WorldGeneration/Systems/ChunkMeshBuilderSystem.cs` | Greedy mesh scheduling |
| `Assets/_Project/Scripts/WorldGeneration/Systems/ChunkRenderSystem.cs` | Frustum cull + GPU draw |
| `Assets/_Project/Scripts/WorldGeneration/Systems/ChunkManagerSystem.cs` | Chunk destruction/cleanup |
| `Assets/_Project/Scripts/WorldGeneration/Jobs/GreedyMeshJob.cs` | Mesh generation algorithm |
| `Assets/_Project/Scripts/NativeMesh.cs` | Native container for mesh data (job-safe) |

## DOTS Conventions

- All jobs must be `[BurstCompile]`, operate only on blittable types / NativeContainers.
- Chain job handles via `JobHandle.CombineDependencies`; complete before disposal.
- Never access `NativeArray` or `NativeHashMap` on main thread while job using it is scheduled.
- ECS structural changes (add/remove component, destroy entity) must use `EntityCommandBuffer` — schedule ECBs at correct sync point.
- `ChunkManagerSystem` (OrderLast) is safe point for all disposals — don't dispose chunk data in other systems.