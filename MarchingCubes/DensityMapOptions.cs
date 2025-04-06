
using System;
using UnityEngine;

[Serializable]
public class DensityMapOptions
{
    [Range(-0.5f, 0.5f), Tooltip("Defines the cutoff point for the surface. Voxels below this value get filled in, voxels above it get carved out.")]
    public float ISOLevel = 0.5f;

    [Range(1, 12), Tooltip("Controls how many layers of noise are stacked. More octaves = more detail, but also more compute-heavy.")]
    public int Octaves = 5;

    [Range(0f, 1f), Tooltip("Scales the noise map. Lower values zoom in for big landmasses, higher values zoom out for finer features.")]
    public float NoiseScale = 1f;

    [Range(1f, 150f), Tooltip("Multiplies the overall noise height. Think of this like turning up the terrain’s 'volume'.")]
    public float NoiseMultiplier = 1f;

    [Range(0f, 25f), Tooltip("Base height of terrain features. Higher amplitude = taller hills and deeper valleys.")]
    public float Amplitude = 1f;

    [Range(0f, 3f), Tooltip("How quickly the noise changes over space. Higher frequency = more rapid bumps and dips.")]
    public float Frequency = 1f;

    [SerializeField, Header("Coloring")]
    public Gradient SurfaceColorRange;
    public float StartSurfaceLevel;
}