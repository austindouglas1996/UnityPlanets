using System;
using UnityEngine;

/// <summary>
/// Defines a generator that produces a 3D density map for marching cube terrain.
/// </summary>
public interface IDensityMapGenerator
{
    /// <summary>
    /// The noise and shaping options used when generating density values.
    /// </summary>
    DensityMapOptions Options { get; }

    /// <summary>
    /// Generates a 3D density map for a chunk at the given coordinates.
    /// </summary>
    /// <param name="chunkSize">The size of the chunk along one axis (assumes cubic chunks).</param>
    /// <param name="chunkCoordinates">The chunk's position in chunk space.</param>
    /// <returns>A 3D array of floats representing the density values of the chunk.</returns>
    DensityMap Generate(int chunkSize, Vector3Int chunkCoordinates, int lodIndex);
}