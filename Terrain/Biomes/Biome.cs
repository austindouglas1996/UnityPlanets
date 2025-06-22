using System;
using UnityEngine;

[Serializable]
public class Biome
{
    public string Name;
    public float MinSurface;
    public float MaxSurface;
    public Gradient SurfaceColorRange;
}