using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

/// <summary>
/// Contains config data for how chunks are sized, typed, and generated.
/// Passed into generators and factories.
/// </summary>
public interface IChunkConfiguration
{
    /// <summary>
    /// Size (in voxels) for one side of a chunk.
    /// </summary>
    int ChunkSize { get; }

    /// <summary>
    /// Size (in voxels) for how far in each direction chunks should be rendered.
    /// </summary>
    ChunkRenderRange RenderDistanceInChunks { get; }

    /// <summary>
    /// Density Options used for marching cubes generator.
    /// </summary>
    DensityMapOptions MapOptions { get; }

    /// <summary>
    /// Biomes to use when generating chunks.
    /// </summary>
    List<Biome> Biomes { get; }

    /// <summary>
    /// Chunk modifiers to use when generating chunks.
    /// </summary>
    List<ITerrainModifier> Modifiers { get; }
}
