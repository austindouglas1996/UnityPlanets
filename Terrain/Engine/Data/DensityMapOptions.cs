using System;
using UnityEngine;

[Serializable]
public struct DensityMapOptions
{
    [Header("Global Settings")]

    [Tooltip("Base sized used for all chunk rendering. LOD of chunks will further expand this value.")]
    public int ChunkSize;

    [Tooltip("Seed used for all noise generation. Keeps terrain consistent across sessions.")]
    public int Seed;

    [Tooltip("Defines the surface cutoff. Voxels below this value are solid, above are air.")]
    [Range(-1f, 1f)]
    public float ISOLevel;

    [Header("Continent Sculpting")]
    [Tooltip("Controls how large continents and oceans are.")]
    public float ContinentFrequency;

    [Tooltip("How tall continents become above sea level.")]
    public float ContinentAmplitude;

    [Header("Terrain Detail Sculpting")]
    [Tooltip("Controls size of local bumps and dips.")]
    public float DetailFrequency;

    [Tooltip("Strength of small hills and details.")]
    public float DetailAmplitude;

    [Header("Flatness Control")]
    [Tooltip("Controls large flat zones (plains, deserts).")]
    public float FlatnessFrequency;

    [Tooltip("How aggressively flat areas are smoothed.")]
    public float FlatnessStrength;

    [Tooltip("Overall vertical scale for terrain.")]
    public float TotalHeightScale; 
}
