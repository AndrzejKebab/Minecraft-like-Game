# FastNoise2 for Unity (UPM Package)

[FastNoise2](https://github.com/Auburn/FastNoise2) v1.1.1 packaged for Unity Package Manager (UPM). SIMD-accelerated noise generation with a type-safe builder API, base64 encode/decode compatible with C++ NoiseTool, and full Burst/Jobs/IL2CPP compatibility.

For native texture containers and sampling utilities, see the optional companion package [`NativeTexture`](https://github.com/api-haus/com.api-haus.native-texture).

---

## Features

- **Type-Safe Builder API**:
  - `Noise.*` factory methods (`Perlin`, `Simplex`, `Value`, `Cellular*`, etc.)
  - `NoiseNode` fluent chaining: fractal (`Fbm`, `Ridged`), blend (`Min`, `Max`, `Fade`), domain warp (`DomainWarpGradient`, `DomainWarpSimplex`), value modifiers (`Remap`, `Terrace`, `Abs`, `PingPong`)
  - Arithmetic operators (`+`, `-`, `*`, `/`, `%`) on noise nodes
  - `Hybrid` parameter type — accepts either a `float` constant or a `NoiseNode`
  - Specialized nodes: `GradientNode`, `DistanceToPointNode`, `CellularValueNode`, `CellularDistanceNode`, `CellularLookupNode`, `DomainWarpNode`

- **Encode/Decode**:
  - `NoiseNode.Encode()` — serializes the noise graph to base64 binary format
  - `NoiseNode.Decode(string)` — deserializes a base64 encoded node tree
  - Format is compatible with C++ NoiseTool

- **Job System Integration**:
  - Full Burst/Jobs/IL2CPP compatibility for noise generation
  - Native interop via `FastNoise` handle type

- **Authoring Tools**:
  - `BakedNoiseTextureAsset` for creating and configuring baked noise textures
  - Property drawers and custom editors for noise assets

- **Cross-Platform Native Support**:
  - Optimized native libraries for Windows, macOS, Linux, iOS, and Android
  - Universal architecture support (x64, arm64)

---

## Platform Support

- ✅ Linux x64
- ✅ macOS Universal, macOS Intel
- ✅ Windows x64
- ✅ Android x64
- ✅ iOS arm64

---

## Dependencies

| Dependency | Version | Required |
|---|---|---|
| Unity.Burst | — | Runtime ref |
| Unity.Collections | — | Runtime ref |
| Unity.Mathematics | — | Runtime ref |
| [`NativeTexture`](https://github.com/api-haus/com.api-haus.native-texture) | — | Optional — native texture containers + FN2 sampling bridge |

---

## Getting Started

### Installation via OpenUPM (recommended)

```bash
openupm add com.api-haus.fastnoise2
```

### Installation via Unity Package Manager (UPM)

1. Open Unity and navigate to `Window → Package Manager`.
2. Click the `+` button and select `Add package from git URL...`.
3. Paste the following URL and click `Add`:

```
https://github.com/api-haus/com.api-haus.fastnoise2.git
```

Unity will automatically download and install the package.

---

## Usage Examples

### Builder API

```csharp
using FastNoise2.Generators;

// Simple ridged perlin noise
NoiseNode ridged = Noise.Perlin().Ridged();
string encoded = ridged.Encode();

// Complex graph with domain warp and blending
NoiseNode graph = Noise.Simplex(featureScale: 200f)
    .DomainWarpGradient(warpAmplitude: 50f)
    .Fbm(octaves: 4, lacunarity: 2f)
    .Min(Noise.Constant(0.5f))
    .Terrace(smoothness: 0.2f);

// Build a native FastNoise handle for generation
using FastNoise fn = FastNoise.FromEncodedNodeTree(graph.Encode());
```

### Encode/Decode Round-Trip

```csharp
using FastNoise2.Generators;

NoiseNode original = Noise.Perlin().Fbm(octaves: 5);

// Encode to base64 (compatible with C++ NoiseTool)
string encoded = original.Encode();

// Decode back to a NoiseNode
NoiseNode decoded = NoiseNode.Decode(encoded);

// Re-encode produces the same string
Debug.Log(encoded == decoded.Encode()); // True
```

### Direct Generation

```csharp
using FastNoise2.Bindings;

using FastNoise fn = FastNoise.FromEncodedNodeTree("BxA9Cle/GGZmZj8E");

float[] noiseOutput = new float[512 * 512];
FastNoise.OutputMinMax minMax = fn.GenUniformGrid2D(
    noiseOutput, 0, 0, 512, 512, 0.02f, 1337);

Debug.Log($"Noise bounds: Min={minMax.min}, Max={minMax.max}");
```

### With NativeTexture Integration

When the optional [`NativeTexture`](https://github.com/api-haus/com.api-haus.native-texture) package is installed, you can generate noise directly into native texture containers. See the NativeTexture package README for extension method details.

```csharp
using FastNoise2.Bindings;
using NativeTexture;
using NativeTexture.FastNoise2;
using Unity.Collections;
using Unity.Mathematics;

using FastNoise fn = FastNoise.FromEncodedNodeTree("BxA9Cle/GGZmZj8E");

var noiseTexture = new NativeTexture2D<float>(new int2(512, 512), Allocator.TempJob);

fn.GenUniformGrid2D(
    noiseTexture,
    out FastNoise.OutputMinMax minMax,
    0, 0,
    noiseTexture.Width, noiseTexture.Height,
    0.02f, 0.02f,
    1337);

Debug.Log($"Noise bounds: Min={minMax.min}, Max={minMax.max}");

Texture2D texture = new Texture2D(512, 512, TextureFormat.RFloat, false);
noiseTexture.ApplyTo(texture);
noiseTexture.Dispose();
```

---

## Assemblies

| Assembly | Notes |
|---|---|
| `FastNoise2.Runtime` | `autoReferenced: false`, unsafe, namespace `FastNoise2` + `FastNoise2.Generators` |
| `FastNoise2.Authoring` | `autoReferenced: false`, namespace `FastNoise2` |
| `FastNoise2.Editor` | Editor-only, `autoReferenced: false`, namespace `FastNoise2` |

All assemblies have `autoReferenced: false` — consumers must add explicit assembly references.

---

## Version Mapping

| UPM Package | FastNoise2 (C++) |
|---|---|
| \>= v1.1.0 | [v1.1.1](https://github.com/Auburn/FastNoise2/releases/tag/v1.1.1) |
| v1.0.0 | [v1.0.1](https://github.com/Auburn/FastNoise2/releases/tag/v1.0.1) |

---

## Contributing

Contributions are welcome! Please submit pull requests or open issues on the [GitHub repository](https://github.com/Auburn/FastNoise2).

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
