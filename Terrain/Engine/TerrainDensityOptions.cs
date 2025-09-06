using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Subvarient types for better rendering.
/// </summary>
public enum SubVariant
{
    /// <summary>
    /// Endless land, no roof or floor.
    /// </summary>
    LandMass,

    /// <summary>
    /// A sphere of land, no roof but has a center.
    /// </summary>
    Planet,

    /// <summary>
    /// Cave systems, roof and floor.
    /// </summary>
    Cave
}

/// <summary>
/// First/pass base terrain layer. Shapes the big landmass, sprinkles broad detail,
/// and modulates detail with a flatness mask. Later layers (rivers, caves, mountains)
/// can stack on top.
/// </summary>
[Serializable, StructLayout(LayoutKind.Sequential)]
public struct TerrainDensityOptions
{
    [Header("Global")]
    [Tooltip("Logical voxel width of a chunk (before LOD).")]
    public int ChunkSize;

    [Tooltip("Noise seed. Use this to derive offsets so worlds are stable per seed.")]
    public int Seed;

    [Tooltip("Those chosen subvariant for choosing the correct type of density options.")]
    public SubVariant Variant;

    [Tooltip("Iso threshold used by marching. Keep if your meshing kernel reads it.")]
    [Range(-1f, 1f)]
    public float ISOLevel;

    [Header("Base Landmass (continents")]
    [Tooltip("How wide the continents are. Lower = bigger continents.")]
    public float BaseFreq;    

    [Tooltip("How much the base layer lifts terrain up.")]
    public float BaseGain;

    [Header("Oceans & Coasts")]
    [Tooltip("Land/ocean split. Higher = more ocean. Typical 0.45–0.6.")]
    [Range(0f, 1f)]
    public float SeaLevel;

    [Tooltip("Width of the coast transition. Smaller = sharper coastlines.")]
    [Range(0f, 1f)]
    public float CoastWidth;

    [Tooltip("How far below baseline oceans are carved. Negative values = carve downward.")]
    public float OceanDepth;

    [Header("Broad Detail")]
    [Tooltip("Size of large-scale bumps on top of the base.")]
    public float DetailFreq;  

    [Tooltip("Strength of those broad details.")]
    public float DetailGain;

    [Header("Flatness Mask")]
    [Tooltip("How large the flat regions run across the map.")]
    public float FlatMaskFreq; 

    [Tooltip("Higher -> stronger flattening in masked zones.")]
    public float FlatMaskPower; 

    [Header("Vertical Scale")]
    [Tooltip("Global height scale for this layer after normalization.")]
    public float ElevationScale;

    [Header("Domain Offsets")]
    [Tooltip("XYZ offset for generation. A simple way to control the position as generation happens at 0,0,0.")]
    public Vector3 PositionOffset;

    [Tooltip("XYZ offset for base landmass noise domain.")]
    public Vector3 BaseOffset;

    [Tooltip("XYZ offset for detail domain (replaces +1234).")]
    public Vector3 DetailOffset;

    [Tooltip("XYZ offset for flatness domain (replaces +5555).")]
    public Vector3 FlatMaskOffset;

    [HideInInspector]
    private Vector3 _Padding;
}
