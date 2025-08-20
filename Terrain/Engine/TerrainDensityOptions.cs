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

    [Tooltip("A debug tool to show the LOD of each chunk."), Range(0,1)]
    public int LODHeatMap;

    [Tooltip("Those chosen subvariant for choosing the correct type of density options.")]
    public SubVariant Variant;

    [Tooltip("Iso threshold used by marching. Keep if your meshing kernel reads it.")]
    [Range(-1f, 1f)]
    public float ISOLevel;

    [Header("Base Landmass (continents/oceans)")]
    [Tooltip("How wide the continents are. Lower = bigger continents.")]
    public float BaseFreq;    

    [Tooltip("How much the base layer lifts terrain up.")]
    public float BaseGain;  

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
    [Tooltip("XY offset for base landmass noise domain.")]
    public Vector2 BaseOffset;

    [Tooltip("XY offset for detail domain (replaces +1234).")]
    public Vector2 DetailOffset;

    [Tooltip("XY offset for flatness domain (replaces +5555).")]
    public Vector2 FlatMaskOffset;

    [HideInInspector]
    private Vector2 _Padding;
}
