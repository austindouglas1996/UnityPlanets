using Unity.Mathematics;
using UnityEngine;
using UnityEngine.LightTransport;

public class OceanBiomeNoise : IBiome
{
    public OceanBiomeNoise(DensityMapOptions options)
    {
        this.DensityMapOptions = options;
    }

    public int Id { get; } = 1;
    public string Name => "Ocean";
    public float PreferredHeight => -32;
    public DensityMapOptions DensityMapOptions { get; private set; }

    readonly float WaterDepth = 24f;     // how many units below terrain
    readonly float RippleScale = 0.01f;   // small seafloor bumps
    readonly float RippleAmp = 5f;      // amplitude of those bumps

    public float Evaluate(float baseVal, Vector3 worldPos)
    {
        // 1) sample a little seafloor noise
        float ripple = Perlin.Fbm(
            (worldPos.x + DensityMapOptions.Seed) * RippleScale,
            (worldPos.y + DensityMapOptions.Seed) * RippleScale,
            (worldPos.z + DensityMapOptions.Seed) * RippleScale,
            DensityMapOptions.Octaves
        ) * RippleAmp;

        // 2) start from the terrain baseline
        float oceanVal = baseVal
                       - WaterDepth   // drop 64 units below land
                       + ripple;      // add tiny seafloor bumps

        // 3) Return that—this will cross the ISO threshold downward
        return oceanVal;
    }

}