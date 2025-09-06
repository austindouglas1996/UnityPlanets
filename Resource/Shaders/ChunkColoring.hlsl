#ifndef CHUNK_COMMON_COLORING_INCLUDED
#define CHUNK_COMMON_COLORING_INCLUDED

#include "ChunkCommon.hlsl"
#include "Lib/PerlinNoise.hlsl"

// Structured buffer of all active biome definitions.
StructuredBuffer<ChunkBiomeData> BiomeColors;
int _BiomeCount;

// Get a color based on LOD index (green -> red, like LodColor)
float4 GetLodColor(int lod)
{
    if (lod == 10)
        return float4(1.0, 0.0, 1.0, 1.0); // Magenta = edge test
    if (lod == 11)
        return float4(1.0, 0.0, 0.0, 1.0);

        switch (lod)
        {
            case 0:
                return float4(1.0, 0.0, 0.0, 1.0); // Red
            case 1:
                return float4(1.0, 0.5, 0.0, 1.0); // Orange
            case 2:
                return float4(0.0, 0.0, 1.0, 1.0); // Blue
            case 3:
                return float4(0.0, 1.0, 0.0, 1.0); // Green
            case 4:
                return float4(0.5, 0.8, 1.0, 1.0); // Light blue (snowy look)
        }

    // Default/fallback
    return float4(1.0, 1.0, 1.0, 1.0);
}

float4 GetColorByIndex(ChunkBiomeData biome, int idx)
{
    if (idx == 0)
        return biome.Highlight;
    if (idx == 1)
        return biome.Light;
    if (idx == 2)
        return biome.MidLight;
    if (idx == 3)
        return biome.Mid;
    if (idx == 4)
        return biome.Dark;
    return biome.Shadow;
}

float BiomeHeightWeight(ChunkBiomeData b, float y, float feather)
{
    // center/halfWidth for a neat symmetric falloff
    float center = 0.5 * (b.minSurface + b.maxSurface);
    float halfWidth = 0.5 * (b.maxSurface - b.minSurface);

    // distance from band center
    float d = abs(y - center);

    // Inside the "core" band (<= halfWidth - feather) => weight ~ 1
    // Outside the band (>= halfWidth + feather)       => weight ~ 0
    // Feather zone blends between.
    float edge0 = max(halfWidth - feather, 0.0);
    float edge1 = halfWidth + feather;

    // Map d from [edge0, edge1] -> [0,1], clamp
    float t = saturate((d - edge0) / max(edge1 - edge0, 1e-5));

    // 1 inside, 0 outside, smooth in between
    return 1.0 - t;
}

void FindTopTwoBiomesByHeight(float y, out int bi0, out int bi1, out float w0, out float w1)
{
    // Feather in world units. Tune to taste.
    // Example: 2–6 meters feels good for wide bands.
    const float FEATHER = 4.0;

    bi0 = 0;
    bi1 = 0;
    w0 = 0.0;
    w1 = 0.0;

    // One pass, track best and second best
    [loop]
    for (int i = 0; i < _BiomeCount; i++)
    {
        float wi = BiomeHeightWeight(BiomeColors[i], y, FEATHER);

        // Insert-sort into (bi0,w0) and (bi1,w1)
        if (wi > w0)
        {
            w1 = w0;
            bi1 = bi0;
            w0 = wi;
            bi0 = i;
        }
        else if (wi > w1)
        {
            w1 = wi;
            bi1 = i;
        }
    }

    // Normalize the top two so they sum to 1 (avoid NaN when both 0)
    float sum = max(w0 + w1, 1e-5);
    w0 /= sum;
    w1 /= sum;
}

float3 SampleBiomePalette(ChunkBiomeData biome, float2 xz)
{
    // Tunables
    const float ColorFreq = 0.002; // lower = bigger blobs
    const float2 ColorSeed = float2(13.1, 71.7);

    float2 p2 = xz * ColorFreq + ColorSeed;
    float n = N01(fbm3D(float3(p2, 0.0), 3));

    // 6-step palette indexing with lerp between adjacent steps
    const int N = 6;
    float idxf = n * (N - 1);
    int i0 = (int) floor(idxf);
    int i1 = min(i0 + 1, N - 1);
    float w = frac(idxf);

    return lerp(GetColorByIndex(biome, i0).rgb, GetColorByIndex(biome, i1).rgb, w);
}

float3 GetTerrainColor(float3 normalWS, float3 positionWS)
{
    int b0, b1;
    float w0, w1;
    
    if (SubVariant == SUBVARIANT_PLANET)
    {
        float y = length(positionWS - PlanetCenter) - PlanetRadius;
        FindTopTwoBiomesByHeight(y, b0, b1, w0, w1);
    }
    else
    {
        FindTopTwoBiomesByHeight(positionWS.y, b0, b1, w0, w1);
    }

    float3 c0 = SampleBiomePalette(BiomeColors[b0], positionWS.xz);
    float3 c1 = SampleBiomePalette(BiomeColors[b1], positionWS.xz);

    return c0 * w0 + c1 * w1;
}

#endif