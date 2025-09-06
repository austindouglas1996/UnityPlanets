using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Biome
{
    public string Name;
    public float MinSurface;
    public float MaxSurface;
    public float MinTemp;
    public float MaxTemp;

    public Color Highlight;
    public Color Light;
    public Color MidLight;
    public Color Mid;
    public Color Dark;
    public Color Shadow;
}