using UnityEngine;

public class OceanBiomeNoise : IBiome
{
    public OceanBiomeNoise(DensityMapOptions options)
    {
        this.DensityMapOptions = options;
    }

    public string Name => "Ocean";
    public DensityMapOptions DensityMapOptions { get; private set; }

    public float Evaulate(float baseVal, Vector3 worldPos)
    {
        float sampleFreq = 0.4f * 0.05f;
        float terrainNoise = Perlin.Fbm(
            (worldPos.x + DensityMapOptions.Seed) * sampleFreq,
            (worldPos.y + DensityMapOptions.Seed) * sampleFreq,
            (worldPos.z + DensityMapOptions.Seed) * sampleFreq,
            DensityMapOptions.Octaves
        ) * DensityMapOptions.Amplitude;

        // how far below sea level we want to pull
        float gravityWell = -worldPos.y * 5.5f;

        // carve a little bumpy seafloor for extra decorations later.
        float oceanFloor = gravityWell - (terrainNoise * 20f);

        // return the *lower* of land or ocean, so ocean always undercuts land
        return baseVal + Mathf.Min(baseVal, oceanFloor) - 10;
    }
}