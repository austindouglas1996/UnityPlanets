using UnityEngine;

public abstract class GenericBiome : IBiome
{
    public GenericBiome(BiomeOptions biomeOptions, DensityMapOptions baseOptions)
    {
        this.Id = biomeOptions.Id;
        this.Name = biomeOptions.Name;
        this.BaseMapOptions = baseOptions;
        this.MinSurface = biomeOptions.MinSurface;
        this.SurfaceColorRange = biomeOptions.SurfaceColorRange;
    }

    public int Id { get; protected set; }

    public string Name { get; protected set; }

    public float MinSurface { get; protected set; }

    public DensityMapOptions BaseMapOptions { get; protected set; }

    public Gradient SurfaceColorRange { get; protected set; }

    public abstract float Evaluate(float baseVal, Vector3 worldPos);
}