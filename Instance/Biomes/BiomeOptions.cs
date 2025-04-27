using System;
using UnityEngine;

[Serializable]
public class BiomeOptions
{
    public int Id;
    public string Name;
    public float MinSurface;

    public Gradient SurfaceColorRange;
}