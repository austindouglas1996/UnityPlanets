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
}