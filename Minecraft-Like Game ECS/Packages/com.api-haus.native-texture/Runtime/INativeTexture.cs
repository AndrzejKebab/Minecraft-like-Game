namespace NativeTexture
{
  using Unity.Collections;

  /// <summary>
  /// Represents a native texture interface for handling texture data efficiently using Unity's native collections.
  /// </summary>
  /// <typeparam name="TCoord">The coordinate type used for indexing texture data (e.g., int2, int3).</typeparam>
  /// <typeparam name="TValue">The unmanaged value type stored in the texture (e.g., float).</typeparam>
  public interface INativeTexture<TCoord, TValue>
    where TValue : unmanaged
  {
    /// <summary>
    /// Gets the resolution of the texture.
    /// </summary>
    TCoord Resolution { get; }

    /// <summary>
    /// Gets or sets the texture value at the specified coordinate.
    /// </summary>
    /// <param name="coord">The coordinate of the pixel.</param>
    /// <returns>The value at the specified coordinate.</returns>
    TValue this[TCoord coord] { get; set; }

    /// <summary>
    /// Gets or sets the texture value at the specified linear pixel index.
    /// </summary>
    /// <param name="pixelIndex">The linear index of the pixel.</param>
    /// <returns>The value at the specified index.</returns>
    TValue this[int pixelIndex] { get; set; }

    /// <summary>
    /// Indicates whether the native texture has been created and initialized.
    /// </summary>
    bool IsCreated { get; }

    /// <summary>
    /// Gets the total number of elements in the texture.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Returns the underlying texture data as a NativeArray.
    /// </summary>
    /// <returns>A NativeArray containing the texture data.</returns>
    NativeArray<TValue> AsArray();

    /// <summary>
    /// Gets an unsafe pointer to the underlying texture data.
    /// </summary>
    /// <returns>An unsafe pointer to the texture data.</returns>
    unsafe void* GetUnsafePtr();
  }
}
