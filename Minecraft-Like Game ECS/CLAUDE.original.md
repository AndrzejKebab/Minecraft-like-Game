# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Minecraft-style voxel game built with Unity's **DOTS stack** (ECS + Burst + Job System + Unity Physics). All performance-critical code runs as Burst-compiled jobs over NativeContainers — avoid introducing MonoBehaviour logic into hot paths.

Unity version: confirmed via ProjectSettings. Packages managed via `Packages/manifest.json`.

## Build & Development

There are no CLI build scripts. Development happens entirely through the Unity Editor:
- Open `Minecraft-Like Game ECS.sln` in Rider or Visual Studio to get IDE support.
- Enter Play Mode in the Unity Editor to run the game.
- Burst compilation happens automatically; use **Jobs → Burst → Open Inspector** to verify job compilation.
- Use **Window → Analysis → Profiler** and **Entity Debugger** (`Window → Entities → Hierarchy`) to debug ECS state at runtime.

## Architecture

### Chunk Pipeline (Core Data Flow)

Every frame, chunks flow through a strict dependency chain:

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
- Max concurrent terrain generation jobs: **1** (`GameSettings.MAX_CONCURRENT_JOBS`)

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
2. **CavesPassJob** — carves cave volumes into terrain
3. **DecorationPassJob** — places trees and ores; uses a **4-color checkerboard wave** so adjacent chunks can decorate in parallel without write conflicts

### Greedy Meshing (GreedyMeshJob)

Merges coplanar, same-block faces into larger quads to minimize triangle count (~60-70% reduction vs. naive cube meshing). Requires neighbor chunk `BlockData` for correct face culling at chunk borders — mesh jobs depend on all 6 neighbors being populated first.

### Rendering (ChunkRenderSystem)

- **Frustum culling** per chunk before issuing any draw calls
- **GPU indirect draw** via `GraphicsBuffer` (StructuredBuffer in shader) — chunk origin + vertex buffer passed to shader, no per-vertex CPU upload each frame
- Solid and fluid meshes rendered with separate materials/passes

### Character Systems

- `FirstPersonCharacterPhysicsUpdateSystem` — kinematic controller via Unity.CharacterController
- `PlayerInteractionSystem` — block break/place raycast
- `MainCameraSystem` — follows player entity

### Bootstrap

`GameBootstrap.cs` runs on game start:
- Loads block definitions from `WorldBlockRegistrySingleton`
- Generates texture arrays (`TextureArrayGenerator`) for the block atlas
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

- All jobs must be `[BurstCompile]` and operate only on blittable types / NativeContainers.
- Job handles must be chained (`JobHandle.CombineDependencies`) and completed before disposal.
- Never access `NativeArray` or `NativeHashMap` on the main thread while a job using it is scheduled.
- ECS structural changes (add/remove component, destroy entity) must happen via `EntityCommandBuffer` — schedule ECBs at the correct sync point.
- `ChunkManagerSystem` (OrderLast) is the safe point for all disposals — do not dispose chunk data in other systems.
