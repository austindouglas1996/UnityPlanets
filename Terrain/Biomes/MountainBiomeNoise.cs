using UnityEngine;

public class MountainBiomeNoise : IBiome
{
    public MountainBiomeNoise(DensityMapOptions options)
    {
        this.DensityMapOptions = options;
    }

    public string Name => "Mountain";
    public DensityMapOptions DensityMapOptions {  get; private set; }

    public float Evaulate(float baseVal, Vector3 worldPos)
    {
        // Use 2D FBM for mountain *shapes*, not 3D — we want large ridges on the surface, not caves
        float sampleFreq = 0.002f;

        float mountainShape = Perlin.Fbm(
            (worldPos.x + DensityMapOptions.Seed) * sampleFreq,
            (worldPos.z + DensityMapOptions.Seed) * sampleFreq,
            0,
            DensityMapOptions.Octaves
        );

        mountainShape = 1f - Mathf.Abs(mountainShape * 2f - 1f); // turn to ridges

        float height = mountainShape * 155f;

        return (baseVal + 25) + height;
    }
}