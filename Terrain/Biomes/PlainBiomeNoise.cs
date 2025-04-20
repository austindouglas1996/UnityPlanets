using UnityEngine;

public class PlainBiomeNoise : IBiome
{
    public PlainBiomeNoise(DensityMapOptions options)
    {
        this.DensityMapOptions = options;
    }

    public string Name => "Plain";
    public DensityMapOptions DensityMapOptions { get; private set; }

    public float Evaulate(float baseVal, Vector3 worldPos)
    {
        return baseVal;
    }
}