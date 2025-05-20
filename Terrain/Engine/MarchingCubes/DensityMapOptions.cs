using System;
using UnityEngine;
public enum NoiseType
{
    Perlin,
    Simplex,
    Ridged,
    Cellular
}

[Serializable]
public class DensityMapOptions
{
    [Header("Global Settings")]
    [Tooltip("Seed used for all noise generation. Keeps terrain consistent across sessions.")]
    public int Seed = 0;

    [Tooltip("Defines the surface cutoff. Voxels below this value are solid, above are air.")]
    [Range(-1f, 1f)]
    public float ISOLevel = 0.0f;

    [Header("Continent Sculpting")]
    [Tooltip("Controls how large continents and oceans are.")]
    public float ContinentFrequency = 0.001f;

    [Tooltip("How tall continents become above sea level.")]
    public float ContinentAmplitude = 200f;

    [Tooltip("Minimum continent noise to be land (otherwise ocean).")]
    [Range(0f, 1f)]
    public float LandThreshold = 0.4f;

    [Header("Mountain Sculpting")]
    [Tooltip("Controls spacing of mountain chains.")]
    public float MountainFrequency = 0.004f;

    [Tooltip("Controls height of mountain regions.")]
    public float MountainAmplitude = 140f;

    [Tooltip("Sharpness of mountain ridges (higher = sharper peaks).")]
    public float MountainSharpness = 3f;

    [Header("Terrain Detail Sculpting")]
    [Tooltip("Controls size of local bumps and dips.")]
    public float DetailFrequency = 0.02f;

    [Tooltip("Strength of small hills and details.")]
    public float DetailAmplitude = 25f;

    [Header("Flatness Control")]
    [Tooltip("Controls large flat zones (plains, deserts).")]
    public float FlatnessFrequency = 0.006f;

    [Tooltip("How aggressively flat areas are smoothed.")]
    public float FlatnessStrength = 1f;

    [Header("Terrain Shape Remapping")]
    [Tooltip("Curve to remap terrain height. Controls flatness, hills, and peaks.")]
    public AnimationCurve TerrainShapeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("Overall vertical scale for terrain.")]
    public float TotalHeightScale = 256f;
}
