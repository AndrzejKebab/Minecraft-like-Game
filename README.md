# Minecraft-like-Game

![LastCommit](https://img.shields.io/github/last-commit/AndrzejKebab/Minecraft-like-Game)

A 3D sandbox game inspired by Minecraft, developed using Unity. This project explores different multithreading techniques to improve game performance and responsiveness.

## Features

- **Voxel-based terrain generation**: Infinite world generation with destructible and constructible blocks.
- **Multithreading support**: Efficient use of multi-core processors to enhance game performance.
- **Three multithreading implementations**:
  - Using **System.Threading**.
  - Using **Unity.Jobs** and **Burst Compiler**.
  - Using **Unity ECS**.

## Branches

### System Threading branch

This branch includes the game version with multithreading implemented using System.Threading for improved compatibility and flexibility. The implementation is based on the tutorial series available [here](https://www.youtube.com/watch?v=h66IN1Pndd0&list=PLVsTSlfj0qsWEJ-5eMtXsYp03Y9yF1dEn).

### Unity Jobs branch

This branch includes the game version with multithreading implemented using Unity.Jobs and Burst Compiler for optimal performance. The implementation is based on the tutorial series available [here](https://www.youtube.com/watch?v=HEqbT-RM4s8&list=PLgji-9GMuqkI77VmFk0Rol4AWKp-OgkGr).

### Unity Jobs Data Oriented

This branch is remake of Unity Jobs using Data Oriented Design.

### ECS

This branch includes the game version with multithreading implemented using Unity's Entity Component System (ECS).

### DEV

This branch is used for testing and experimental features.

## Acknowledgements

- Inspired by Minecraft, developed by Mojang.
- Special thanks to the Unity community for their tutorials and support.
- The System.Threading implementation is based on the tutorial series available [here](https://www.youtube.com/watch?v=h66IN1Pndd0&list=PLVsTSlfj0qsWEJ-5eMtXsYp03Y9yF1dEn).
- The Unity.Jobs implementation is based on the tutorial series available [here](https://www.youtube.com/watch?v=HEqbT-RM4s8&list=PLgji-9GMuqkI77VmFk0Rol4AWKp-OgkGr).
- GPU implementation is based on the project available [here](https://github.com/artnas/UnityVoxelMeshGPU).
