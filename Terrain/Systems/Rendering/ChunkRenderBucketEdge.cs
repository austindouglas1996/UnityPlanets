using System.Collections.Generic;
using System;

/// <summary>
/// A specialized render bucket for handling LOD edge chunks that may require stitching.
/// Inherits from <see cref="ChunkRenderBucket"/> but allows for future customization
/// of generation behavior for edge cases (chunks near LOD transitions).
/// </summary>
public class ChunkRenderBucketEdge : ChunkRenderBucket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkRenderBucketEdge"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of chunks this bucket can hold.</param>
    /// <param name="rebuildThreshhold">The number of removed chunks that triggers a rebuild.</param>
    /// <param name="chunkGenerator">The generator responsible for chunk processing.</param>
    public ChunkRenderBucketEdge(int capacity, int rebuildThreshhold, IChunkGenerator chunkGenerator)
        : base(capacity, rebuildThreshhold, chunkGenerator)
    {
    }

    /// <summary>
    /// Core generation logic for edge chunks.
    /// Currently defers to the base implementation, but can be overridden in the future
    /// to provide specialized behavior for LOD stitching or multi-chunk awareness.
    /// </summary>
    /// <param name="items">The list of chunk keys to generate.</param>
    /// <param name="onDone">Callback to invoke when generation is complete.</param>
    protected override void GenerateCore(List<ChunkKey> items, Action<ChunkRenderBatch> onDone)
    {
        ChunkGenerator.DispatchEdgeGeneration(items, onDone);
    }
}
