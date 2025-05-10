using System;
using UnityEngine;

[Serializable]
public class Biome
{
    public string Name;
    public float MinSurface;
    public float MaxSurface;
    public Gradient SurfaceColorRange;

    public Color EvaluateColor(float height)
    {
        float t = Mathf.InverseLerp(MinSurface, MaxSurface, height);
        return SurfaceColorRange.Evaluate(t);
    }
}