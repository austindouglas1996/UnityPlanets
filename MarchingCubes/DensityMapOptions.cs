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
    [Tooltip("Seed used for noise generation. Keeps terrain consistent across sessions.")]
    public int Seed = 0;

    [Tooltip("Type of noise function to use.")]
    public NoiseType SelectedNoise = NoiseType.Perlin;

    [Range(-0.5f, 0.5f), Tooltip("Defines the cutoff point for the surface. Voxels below this value get filled in, voxels above it get carved out.")]
    public float ISOLevel = 0.5f;

    [Range(1, 12), Tooltip("Controls how many layers of noise are stacked. More octaves = more detail, but also more compute-heavy.")]
    public int Octaves = 5;

    [Range(0f, 1f), Tooltip("Scales the noise map. Lower values zoom in for big landmasses, higher values zoom out for finer features.")]
    public float NoiseScale = 1f;

    [Range(1f, 500f), Tooltip("Multiplies the overall noise height. Think of this like turning up the terrain’s 'volume'.")]
    public float NoiseMultiplier = 1f;

    [Range(0f, 25f), Tooltip("Base height of terrain features. Higher amplitude = taller hills and deeper valleys.")]
    public float Amplitude = 1f;

    [Range(0f, 3f), Tooltip("How quickly the noise changes over space. Higher frequency = more rapid bumps and dips.")]
    public float Frequency = 1f;

    [Tooltip("Offsets the noise sampling position. Useful for scrolling or layering multiple sources.")]
    public Vector3 Offset = Vector3.zero;

    [Tooltip("Applies a falloff curve to terrain edges. Useful for islands or floating chunks.")]
    public AnimationCurve FalloffCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Range(0f, 1f), Tooltip("Threshold below which the terrain is flattened. Useful for water or caves.")]
    public float FlattenThreshold = 0.2f;

    [SerializeField, Header("Coloring")]
    public Gradient SurfaceColorRange;
    public float StartSurfaceLevel;
}