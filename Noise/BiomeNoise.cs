using UnityEngine;
using UnityEngine.LightTransport;

public class BiomeNoise
{
    // your ordered list of biomes, each knows how to tweak baseVal/worldPos
    private readonly IBiome[] _biomes;
    private readonly float _biomeScale;
    private readonly float _seed;

    public BiomeNoise(IBiome[] biomes, float biomeScale, float seed)
    {
        _biomes = biomes;
        _biomeScale = biomeScale;
        _seed = seed;
    }

    public float Evaluate(float baseVal, Vector3 worldPos)
    {
        int M = _biomes.Length;
        float regionSize = 200f;
        float fade = 50f;   // 20 units to blend

        // which region
        int i = Mathf.FloorToInt(worldPos.x / regionSize);
        i = Mathf.Clamp(i, 0, M - 1);

        // next region over
        int j = Mathf.Clamp(i + 1, 0, M - 1);

        // local position inside [0..regionSize)
        float localX = worldPos.x - i * regionSize;

        // in the first fade zone? blend from previous→current
        if (localX > regionSize - fade && i < M - 1)
        {
            float w = (localX - (regionSize - fade)) / fade; // 0 at boundary, 1 at band end
            float a = _biomes[i].Modify(baseVal, worldPos);
            float b = _biomes[j].Modify(baseVal, worldPos);
            return Mathf.Lerp(a, b, w);
        }
        // fully inside this region
        else
        {
            return _biomes[i].Modify(baseVal, worldPos);
        }
    }

    public Color EvaluateColor(Vector3 worldPos)
    {
        int M = _biomes.Length;
        float regionSize = 200f;
        float fade = 20f;

        int i = Mathf.FloorToInt(worldPos.x / regionSize);
        i = Mathf.Clamp(i, 0, M - 1);
        int j = Mathf.Clamp(i + 1, 0, M - 1);
        float localX = worldPos.x - i * regionSize;

        if (localX > regionSize - fade && i < M - 1)
        {
            float w = (localX - (regionSize - fade)) / fade;
            Color a = _biomes[i].GetColor();
            Color b = _biomes[j].GetColor();
            return Color.Lerp(a, b, w);
        }
        else
        {
            return _biomes[i].GetColor();
        }
    }


}

/// <summary>
/// Every biome implements this to tweak the density field.
/// Example: plains might just return baseVal + tiny FBM,
/// mountains add ridged noise * height, swamps flatten & add 2D lakes, etc.
/// </summary>
public interface IBiome
{
    float Modify(float baseVal, Vector3 worldPos);
    Color GetColor();
}

/// example: a high‑ridge mountain biome
public class MountainBiome : IBiome
{
    private readonly float _ridgeScale;
    private readonly float _ridgeStrength;
    private readonly float _seed;

    public MountainBiome(float ridgeScale, float ridgeStrength, float seed)
    {
        _ridgeScale = ridgeScale;
        _ridgeStrength = ridgeStrength;
        _seed = seed;
    }

    public float Modify(float baseVal, Vector3 worldPos)
    {
        // Use 2D FBM for mountain *shapes*, not 3D — we want large ridges on the surface, not caves
        float sampleFreq = 0.002f; // very low freq = big hills

        float mountainShape = Perlin.Fbm(
            (worldPos.x + _seed) * sampleFreq,
            (worldPos.z + _seed) * sampleFreq,
            0,
            5 // octaves for rich but smooth hills
        );

        // Optional: use ridged FBM for sharper peaks
        mountainShape = 1f - Mathf.Abs(mountainShape * 2f - 1f); // turn to ridges

        // Scale height to something moderate (25f is a solid hill)
        float height = mountainShape * 155f;

        return (baseVal + 25) + height;
    }

    public Color GetColor()
    {
        return Color.green;
    }
}

public class PlainBiome : IBiome
{
    private readonly float _ridgeScale;
    private readonly float _ridgeStrength;
    private readonly float _seed;

    public PlainBiome(float ridgeScale, float ridgeStrength, float seed)
    {
        _ridgeScale = ridgeScale;
        _ridgeStrength = ridgeStrength;
        _seed = seed;
    }

    public float Modify(float baseVal, Vector3 worldPos)
    {
        return baseVal + 25f;
    }

    public Color GetColor()
    {
        return Color.red;
    }
}

public class OceanBiome : IBiome
{
    private readonly float _depthScale;
    private readonly float _depthStrength;
    private readonly float _seed;

    public OceanBiome(float depthScale, float depthStrength, float seed)
    {
        _depthScale = depthScale;
        _depthStrength = depthStrength;
        _seed = seed;
    }

    public float Modify(float baseVal, Vector3 worldPos)
    {
        float sampleFreq = _depthScale * 0.05f;
        float terrainNoise = Perlin.Fbm(
            (worldPos.x + _seed) * sampleFreq,
            (worldPos.y + _seed) * sampleFreq,
            (worldPos.z + _seed) * sampleFreq,
            4
        ) * 8f;

        // how far below sea level we want to pull
        float gravityWell = -worldPos.y * 7.5f;

        // carve a little bumpy seafloor
        float oceanFloor = gravityWell - (terrainNoise * _depthStrength);

        // return the *lower* of land or ocean, so ocean always undercuts land
        return Mathf.Min(baseVal, oceanFloor);
    }



    public Color GetColor()
    {
        return Color.blue;
    }
}
