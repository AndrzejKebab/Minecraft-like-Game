# NativeTexture

`com.api-haus.native-texture` is a Unity package that provides Burst/job-friendly native containers over texture memory with direct pointer access. It enables high-performance CPU-side texture generation, processing, and efficient upload to Unity `Texture2D` or GPU buffers without unnecessary copies.

---

## Features

- **Native Containers**: `NativeTexture2D<T>`, `NativeTexture3D<T>`, `NativeTexture4D<T>` — own a raw buffer or wrap an existing `Texture2D`
- **Unsafe Variants**: `UnsafeTexture2D<T>`, `UnsafeTexture3D<T>`, `UnsafeTexture4D<T>` — minimal-overhead access with bounds-checked `TryRead`/`TryWrite`
- **Format Types**: `byte2/3/4`, `sbyte2/3/4`, `short2/3/4`, `ushort2/3/4` with operator overloads, `FromNormalized()`/`ToNormalized()`, and swizzle properties
- **Read-Only Views**: `.AsReadOnly()` on 2D, 3D, and 4D containers
- **Normalization**: Burst-compiled `NormalizeTextureJob` for `[min,max]` → `[0,1]` remapping
- **Sampling Extensions**: `ReadPixel`, `ReadPixelBilinear`, `Contains`, `Swap`
- **Utilities**: `MipUtility`, `NativeTextureUnsafeUtility`, `IndexCoordUtils`, `TextureBoundsUtility`
- **FastNoise2 Integration**: Optional bridge assembly with extension methods for noise generation into native textures

---

## Installation

### Via OpenUPM (recommended)

```bash
openupm add com.api-haus.native-texture
```

### Via Unity Package Manager (UPM)

1. Open Unity and navigate to `Window → Package Manager`.
2. Click the `+` button and select `Add package from git URL...`.
3. Paste the following URL and click `Add`:

```
https://github.com/api-haus/com.api-haus.native-texture.git
```

### Dependencies

| Dependency | Version |
|---|---|
| `com.unity.collections` | 2.6.3 |
| `com.unity.mathematics` | 1.3.3 |

Runtime references: `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`

---

## Core API

### NativeTexture2D\<T\>

Namespace: `NativeTexture`

**Construction:**
```csharp
// Wrap an existing Unity texture
var pixels = new NativeTexture2D<float4>(unityTexture);

// Own a raw buffer
var pixels = new NativeTexture2D<float>(new int2(512, 512), mipCount: 1, TextureFormat.RFloat, Allocator.Persistent);

// Shorthand (single mip, format inferred from sizeof(T))
var pixels = new NativeTexture2D<float>(new int2(512, 512), Allocator.TempJob);
```

**Properties:** `Width`, `Height`, `Resolution` (int2), `MipCount`, `Length`, `TexelSize`, `IsCreated`

**Access:**
- Indexers: `texture[int index]`, `texture[int2 coord]`, `texture[int x, int y]`, `texture[int x, int y, int mip]`
- `AsArray()` — returns `NativeArray<T>` over the pixel buffer
- `GetUnsafePtr()` / `GetUnsafeReadOnlyPtr()`
- `AsReadOnly()` — returns `NativeTexture2D<T>.ReadOnly`
- `SliceMip(int mipLevel)` — extract a specific mip level

**Upload:**
- `ApplyTo(Texture2D texture, bool updateMipmaps = false)`

**Disposal:**
- `Dispose()` / `Dispose(JobHandle)` for scheduled cleanup

### NativeTexture3D\<T\>

Namespace: `NativeTexture`

**Construction:**
```csharp
// Own a raw 3D buffer
var voxels = new NativeTexture3D<float>(new int3(64, 64, 64), Allocator.Persistent);

// Wrap an existing Texture2D as a 3D volume
var voxels = new NativeTexture3D<float>(unityTexture, new int3(64, 64, 64));
```

**Properties:** `Width`, `Height`, `Depth`, `Resolution` (int3), `Length`, `TexelSize`, `IsCreated`

**Access:**
- Indexers: `texture[int index]`, `texture[int3 coord]`
- `Load(int3 coord)`, `Load(int index, out int3 coord)`
- `AsArray()` — returns `NativeArray<T>`
- `GetUnsafePtr()` / `GetUnsafeReadOnlyPtr()`
- `AsReadOnly()` — returns `NativeTexture3D<T>.ReadOnly`

**Upload:**
- `ApplyTo(Texture2D texture, bool updateMipmaps = false)`

**Disposal:**
- `Dispose()` / `Dispose(JobHandle)`

### NativeTexture4D\<T\>

Namespace: `NativeTexture`

4D native container for noise fields, animation volumes, parameterized textures, etc.

**Construction:**
```csharp
var volume = new NativeTexture4D<float>(new int4(32, 32, 32, 8), Allocator.Persistent);
```

**Properties:** `Width`, `Height`, `Depth`, `WSize`, `Resolution` (int4), `Length`, `TexelSize`, `IsCreated`

**Access:**
- Indexers: `texture[int index]`, `texture[int4 coord]`
- `Load(int4 coord)`, `Load(int index, out int4 coord)`
- `AsArray()` — returns `NativeArray<T>`
- `GetUnsafePtr()` / `GetUnsafeReadOnlyPtr()`
- `AsReadOnly()` — returns `NativeTexture4D<T>.ReadOnly`

**Disposal:**
- `Dispose()` / `Dispose(JobHandle)`

### Unsafe Texture Variants

Namespace: `NativeTexture`

Minimal-overhead variants with no safety handles. Created from their corresponding native container via factory methods. All provide bounds-checked `TryRead`/`TryWrite`.

| Type | Factory | Coordinate |
|---|---|---|
| `UnsafeTexture2D<T>` | `UnsafeTexture2DFactory.FromNativeTexture(native2D)` | `int2` |
| `UnsafeTexture3D<T>` | `UnsafeTexture3DFactory.FromNativeTexture(native3D)` | `int3` |
| `UnsafeTexture4D<T>` | `UnsafeTexture4DFactory.FromNativeTexture(native4D)` | `int4` |

```csharp
var native = new NativeTexture2D<float>(new int2(256, 256), Allocator.TempJob);
UnsafeTexture2D<float> unsafeTex = UnsafeTexture2DFactory.FromNativeTexture(native);

if (unsafeTex.TryRead(new int2(10, 20), out float value))
    Debug.Log(value);

unsafeTex.TryWrite(new int2(10, 20), in someValue);
```

**Common properties:** `Width`, `Height`, `Length`, `IsCreated` (3D adds `Depth`; 4D adds `Depth`, `WSize`)

---

## Format Types

Namespace: `NativeTexture.Formats`

Custom pixel format types with full operator support:

| Type | Components | Conversions |
|---|---|---|
| `byte2`, `byte3`, `byte4` | unsigned 8-bit | `FromNormalized(floatN)`, `ToNormalized()` |
| `sbyte2`, `sbyte3`, `sbyte4` | signed 8-bit | `FromNormalized(floatN)`, `ToNormalized()` |
| `short2`, `short3`, `short4` | signed 16-bit | `FromNormalized(floatN)`, `ToNormalized()` |
| `ushort2`, `ushort3`, `ushort4` | unsigned 16-bit | `FromNormalized(floatN)`, `ToNormalized()` |

All types include:
- Arithmetic operators: `+`, `-`, `*`, `/`, `%`
- Comparison operators: `<`, `<=`, `>`, `>=`, `==`, `!=`
- Increment/decrement: `++`, `--`
- Implicit/explicit conversions to and from Unity.Mathematics types
- Swizzle properties (e.g., `xy`, `yx`, `xyz`, `wzyx`)

---

## NormalizeTextureJob

Namespace: `NativeTexture.Jobs`

Burst-compiled `IJobParallelFor` that normalizes float texture values from `[min, max]` to `[0, 1]`.

```csharp
using NativeTexture.Jobs;

// Schedule for a 2D texture
JobHandle handle = NormalizeTextureJob.Schedule(texture2D, min: -1f, max: 1f);

// Schedule for a 3D texture
JobHandle handle = NormalizeTextureJob.Schedule(texture3D, min: -1f, max: 1f);
```

---

## Sampling Extensions

Namespace: `NativeTexture.Extensions`

```csharp
using NativeTexture.Extensions;

// Bilinear sampling in normalized UV space
float sample = tex.ReadPixelBilinear(new float2(0.3f, 0.6f));

// Direct pixel read
float pixel = tex.ReadPixel(new int2(100, 200));

// Bounds check
bool inBounds = tex.Contains(new int2(x, y));

// Swap two pixels
tex.Swap(index1, index2);
```

---

## FastNoise2 Integration

Assembly: `NativeTexture.FastNoise2` — auto-compiles when [`com.api-haus.fastnoise2`](https://github.com/api-haus/com.api-haus.fastnoise2) is present (via `versionDefines` → `HAS_FASTNOISE2`).

Namespace: `NativeTexture.FastNoise2`

### Extension Methods on `FastNoise`

```csharp
// 2D uniform grid generation
fn.GenUniformGrid2D(
    NativeTexture2D<T> nativeTexture,
    out FastNoise.OutputMinMax minMax,
    float xOffset, float yOffset,
    int xCount, int yCount,
    float xStepSize, float yStepSize,
    int seed);

// 2D tileable generation
fn.GenTileable2D(
    NativeTexture2D<T> nativeTexture,
    out FastNoise.OutputMinMax minMax,
    int xSize, int ySize,
    float xStepSize, float yStepSize,
    int seed);

// 3D uniform grid generation
fn.GenUniformGrid3D(
    NativeTexture3D<T> nativeTexture,
    out FastNoise.OutputMinMax minMax,
    float xOffset, float yOffset, float zOffset,
    int xCount, int yCount, int zCount,
    float xStepSize, float yStepSize, float zStepSize,
    int seed);
```

### Normalize Extension Methods

```csharp
// Normalize using FastNoise.OutputMinMax directly
JobHandle handle = texture2D.ScheduleNormalize(minMax, dependency);
JobHandle handle = texture3D.ScheduleNormalize(minMax, dependency);
```

### Full Example

```csharp
using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Collections;
using UnityEngine;

using FastNoise fn = FastNoise.FromEncodedNodeTree("DQAFAAAAAAAAQAgAAAAAAD8AAAAAAA==");

var noiseTexture = new NativeTexture2D<float>(new int2(512, 512), Allocator.TempJob);

fn.GenUniformGrid2D(
    noiseTexture,
    out FastNoise.OutputMinMax minMax,
    0, 0,
    noiseTexture.Width, noiseTexture.Height,
    0.02f, 0.02f,
    1337);

// Normalize to [0,1] range
noiseTexture.ScheduleNormalize(minMax).Complete();

var texture = new Texture2D(512, 512, TextureFormat.RFloat, false);
noiseTexture.ApplyTo(texture);
noiseTexture.Dispose();
```

---

## Usage Examples

### Wrap a Unity texture and write a pixel

```csharp
using NativeTexture;
using Unity.Mathematics;
using UnityEngine;

var unityTex = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
var pixels = new NativeTexture2D<float4>(unityTex);
pixels[new int2(128, 128)] = new float4(1, 0, 0, 1);
pixels.ApplyTo(unityTex, updateMipmaps: false);
```

### Allocate a CPU-owned buffer

```csharp
using NativeTexture;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

int w = 256, h = 256;
var pixels = new NativeTexture2D<float>(new int2(w, h), mipCount: 1, TextureFormat.RFloat, Allocator.Persistent);

for (int y = 0; y < h; y++)
for (int x = 0; x < w; x++)
    pixels[new int2(x, y)] = math.saturate(math.sin(x * 0.05f) * math.cos(y * 0.05f));

var target = new Texture2D(w, h, TextureFormat.RFloat, false, true);
pixels.ApplyTo(target);
pixels.Dispose();
```

### Bilinear sampling

```csharp
using NativeTexture;
using NativeTexture.Extensions;
using Unity.Mathematics;

NativeTexture2D<float> tex = /* created elsewhere */;
float sample = tex.ReadPixelBilinear(new float2(0.3f, 0.6f));
```

---

## Notes

- `T` must be unmanaged. Use a matching `TextureFormat` for correct `sizeof(T)` mapping.
- When wrapping an existing `Texture2D`, memory ownership remains with Unity; dispose only buffers you own.
- Burst/jobs ready: safety handles and min/max index constraints; `Dispose(JobHandle)` is available for scheduled cleanup.
- Both runtime assemblies have `autoReferenced: false` — consumers must add explicit assembly references.
- `NativeTexture.FastNoise2` assembly uses `defineConstraints: ["HAS_FASTNOISE2"]` and `versionDefines` to auto-compile only when `com.api-haus.fastnoise2` is present.

---

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `NativeTexture` | `Runtime/NativeTexture.asmdef` | Core containers, formats, jobs, sampling |
| `NativeTexture.FastNoise2` | `Runtime/FastNoise2Integration/NativeTexture.FastNoise2.asmdef` | FN2 bridge, requires `HAS_FASTNOISE2` |

Submodule path: `Packages/com.api-haus.native-texture`
