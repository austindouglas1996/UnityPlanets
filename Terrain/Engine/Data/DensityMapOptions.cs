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

    [Tooltip("Minimum continent noise to be land (otherwise ocean).")]
    [Range(0f, 1f)]
    public float LandThreshold;
    private float _Padding0;

    [Header("Mountain Sculpting")]
    [Tooltip("Controls spacing of mountain chains.")]
    public float MountainFrequency;

    [Tooltip("Controls height of mountain regions.")]
    public float MountainAmplitude;

    [Tooltip("Sharpness of mountain ridges (higher = sharper peaks).")]
    public float MountainSharpness;
    private float _Padding1;

    [Header("Terrain Detail Sculpting")]
    [Tooltip("Controls size of local bumps and dips.")]
    public float DetailFrequency;

    [Tooltip("Strength of small hills and details.")]
    public float DetailAmplitude;
    private float _Padding2;
    private float _Padding3;

    [Header("Flatness Control")]
    [Tooltip("Controls large flat zones (plains, deserts).")]
    public float FlatnessFrequency;

    [Tooltip("How aggressively flat areas are smoothed.")]
    public float FlatnessStrength;
    private float _Padding4;
    private float _Padding5;

    [Tooltip("Overall vertical scale for terrain.")]
    public float TotalHeightScale; 
    private float _Padding6;
    private float _Padding7;
    private float _Padding8;
}
