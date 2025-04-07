using System;
using UnityEngine;

/// <summary>
/// Defines the available types of terrain modification brushes.
/// </summary>
[Serializable]
public enum BrushType
{
    /// <summary>
    /// A round (spherical) brush that applies a falloff effect based on distance from the center.
    /// </summary>
    Round,

    /// <summary>
    /// A square brush that modifies terrain in a cubic area with uniform intensity.
    /// </summary>
    Square,

    /// <summary>
    /// A triangular brush shape (implementation-specific).
    /// </summary>
    Triangle
}


/// <summary>
/// Abstract base class for terrain brushes used to modify voxel density maps.
/// Implementations define how terrain is affected and what area is influenced.
/// </summary>
public abstract class TerrainBrush
{
    /// <summary>
    /// The strength of the terrain modification applied by the brush.
    /// </summary>
    public float Intensity = 0.1f;

    /// <summary>
    /// The isolevel used for evaluating surface threshold in the marchingCubes generation.
    /// </summary>
    public float ISOLevel = 0.5f;

    /// <summary>
    /// The world-space position where a raycast was hit.
    /// </summary>
    public Vector3 WorldHitPoint = Vector3.zero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerrainBrush"/> class.
    /// </summary>
    /// <param name="worldPos">The world position where the brush is applied.</param>
    public TerrainBrush(Vector3 worldPos)
    {
        this.WorldHitPoint = worldPos;
    }

    /// <summary>
    /// Gets the bounds in world space that the brush affects.
    /// Used for chunk intersection tests.
    /// </summary>
    /// <returns>A <see cref="Bounds"/> struct representing the brush's world-space area of effect.</returns>
    public abstract Bounds GetBrushBounds();

    /// <summary>
    /// Calculates the effect strength this brush has on a given voxel.
    /// </summary>
    /// <param name="voxelWorldPosition">The voxel's world-space position.</param>
    /// <param name="brushCenter">The center of the brush in world space.</param>
    /// <returns>The strength of the modification (0 if unaffected).</returns>
    public abstract float GetEffectAmount(Vector3 voxelWorldPosition, Vector3 brushCenter);
}
