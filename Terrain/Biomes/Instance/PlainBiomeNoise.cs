using UnityEngine;

public class PlainBiomeNoise : IBiome
{
    public PlainBiomeNoise(DensityMapOptions options)
    {
        this.DensityMapOptions = options;
    }

    public int Id { get; } = 3;
    public string Name => "Plain";
    public float PreferredHeight => 0;
    public DensityMapOptions DensityMapOptions { get; private set; }

    public float Evaluate(float baseVal, Vector3 worldPos)
    {
        float sampleFreq = 0.015f; // smoother than beach, slight undulation

        float bumps = Perlin.Fbm(
            (worldPos.x + DensityMapOptions.Seed) * sampleFreq,
            (worldPos.z + DensityMapOptions.Seed) * sampleFreq,
            0,
            DensityMapOptions.Octaves
        );

        float height = bumps * 8f; // gentle bumpiness

        return baseVal + height;
    }
}