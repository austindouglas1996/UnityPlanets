using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the context of a specific chunk in the world.
/// Holds its coordinates, LOD level, and shared service references.
/// </summary>
public class ChunkContext
{
    /// <summary>
    /// Initializes a new ChunkContext with position, LOD, and services.
    /// </summary>
    public ChunkContext(Vector3Int coordinates, int LODIndex, IChunkServices services)
        : this(new ChunkKey(coordinates, LODIndex), services)
    {
    }

    /// <summary>
    /// Initializes a new ChunkContext with position, LOD, and services.
    /// </summary>
    public ChunkContext(ChunkKey key, IChunkServices services)
    {
        Services = services;

        WorldPosition = Services.Layout.ToWorld(Key);
        Transform = Matrix4x4.TRS(this.WorldPosition, Quaternion.identity, Vector3.one);
    }

    /// <summary>
    /// Gets or sets the identifer for this context.
    /// </summary>
    public ChunkKey Key { get; private set; }

    /// <summary>
    /// Shared services passed to the chunk (layout, config, generation).
    /// </summary>
    public IChunkServices Services { get; }

    /// <summary>
    /// World position of the chunk, derived from layout and coordinates.
    /// </summary>
    public Vector3 WorldPosition { get; private set; }

    /// <summary>
    /// Creates a transformation matrix.
    /// </summary>
    public Matrix4x4 Transform { get; }

    /// <summary>
    /// Debug string — shows LOD and coordinates for logging.
    /// </summary>
    public override string ToString()
    {
        return $"Chunk LOD:{Key.LODIndex} X:{Key.Coordinates.x} Y:{Key.Coordinates.y} Z:{Key.Coordinates.z}";
    }
}
