using System;
using System.Collections.Generic;

/// <summary>
/// Strategy interface for terrain generation backends (e.g., marching cubes, dual contouring, etc.).
/// Implementations produce draw-ready batches and (optionally) fast surface masks.
/// </summary>
/// <remarks>
/// - Call from the main Unity thread; typical implementations touch GPU buffers.
/// - Lifetime: the callee owns any temporary buffers; the caller owns and must Dispose returned batches.
/// </remarks>
public interface ITerrainGenerator : IDisposable
{
    /// <summary>
    /// Fast prepass to decide which chunks likely contain surface.
    /// Returns one mask word per input job (same ordering/length as <paramref name="keys"/>).
    /// </summary>
    /// <param name="keys">Chunk jobs (coords/LOD) to test for potential surface.</param>
    /// <returns>
    /// Array of mask words; semantics are generator-specific (e.g., nonzero = has surface).
    /// Length equals <paramref name="keys"/>. Never null.
    /// </returns>
    uint[] GetSurfaceMaskChecks(IReadOnlyList<ChunkGenerationJob> keys);

    /// <summary>
    /// Generate a draw-ready batch (triangle append buffer + indirect args + bounds)
    /// for the provided chunk keys. Returns null or throws if the input is empty,
    /// depending on implementation.
    /// </summary>
    /// <param name="keys">Non-empty list of chunk keys to build.</param>
    /// <returns>A <see cref="ChunkRenderBatch"/> the caller must Dispose when done.</returns>
    ChunkRenderBatch GenerateBatch(IReadOnlyList<ChunkKey> keys);

    /// <summary>
    /// Apply updated runtime/editor options to the generator (e.g., density params, biome tables).
    /// Implementations should re-upload constant/structured buffers and invalidate any caches
    /// so subsequent builds reflect the new settings.
    /// </summary>
    void UpdateOptions();
}
