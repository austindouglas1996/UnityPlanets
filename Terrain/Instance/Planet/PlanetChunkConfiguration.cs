using System;
using UnityEngine;

[Serializable]
public class PlanetChunkConfiguration : GenericChunkConfiguration
{
    [SerializeField] private int maxLoadRadius = 128;
    [SerializeField] private int surfaceBuffer = 12;

    public int MaxLoadRadius => maxLoadRadius;
    public int SurfaceBuffer => surfaceBuffer;
}
