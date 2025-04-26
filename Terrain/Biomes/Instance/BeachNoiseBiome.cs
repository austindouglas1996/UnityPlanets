using UnityEngine;

public class BeachBiomeNoise : IBiome
{
    public BeachBiomeNoise(DensityMapOptions options)
    {
        this.DensityMapOptions = options;
    }

    public int Id { get; } = 2;
    public string Name => "Beach";
    public float PreferredHeight => 0f;
    public DensityMapOptions DensityMapOptions { get; private set; }

    public float Evaluate(float baseVal, Vector3 worldPos)
    {
        // 1) sample a little seafloor noise
        float ripple = Perlin.Fbm(
            (worldPos.x + DensityMapOptions.Seed) * 0.01f,
            (worldPos.y + DensityMapOptions.Seed) * 0.01f,
            (worldPos.z + DensityMapOptions.Seed) * 0.01f,
            DensityMapOptions.Octaves
        ) * 5f;

        // 2) start from the terrain baseline
        float oceanVal = baseVal
                       - 6   // drop 64 units below land
                       + ripple;      // add tiny seafloor bumps

        // 3) Return that—this will cross the ISO threshold downward
        return oceanVal;
    }

}