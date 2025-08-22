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
    /// Density Options used for marching cubes generator.
    /// </summary>
    TerrainDensityOptions DensityOptions { get; }

    /// <summary>
    /// Density Options used for planet marching cubes.
    /// </summary>
    PlanetDensityOptions PlanetOptions { get; }

    /// <summary>
    /// Biomes to use when generating chunks.
    /// </summary>
    List<Biome> Biomes { get; }

    /// <summary>
    /// Used for LOD thresholds on chunks for rendering.
    /// </summary>
    LODThresholds LODThresholds { get; }
}
