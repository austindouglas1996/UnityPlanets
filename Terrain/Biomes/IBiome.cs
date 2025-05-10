using UnityEngine;

public interface IBiome
{
    int Id { get; }
    string Name { get; }
    public float MinSurface { get; }
    DensityMapOptions BaseMapOptions { get; }
    Gradient SurfaceColorRange { get; }
}