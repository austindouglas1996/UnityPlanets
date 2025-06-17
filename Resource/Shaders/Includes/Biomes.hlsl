#ifndef BIOME_SHARED_INCLUDED
#define BIOME_SHARED_INCLUDED

struct BiomeData
{
    float minSurface;
    float maxSurface;
    float4 gradientStart;
    float4 gradientEnd;
};

StructuredBuffer<BiomeData> BiomeColors;
int _BiomeCount;
#endif

float4 GetColorForHeight(float height)
{
    BiomeData current = BiomeColors[_BiomeCount - 1];
    BiomeData next = current;
    bool hasUpper = false;

    for (int i = 0; i < _BiomeCount - 1; i++)
    {
        if (height >= BiomeColors[i].minSurface && height < BiomeColors[i + 1].minSurface)
        {
            current = BiomeColors[i];

            if (i + 1 < _BiomeCount)
            {
                next = BiomeColors[i + 1];
                hasUpper = true;
            }
            break;
        }
    }

    // Normalize height within current biome gradient
    float tCurrent = saturate((height - current.minSurface) / max(current.maxSurface - current.minSurface, 0.0001));
    float4 baseColor = lerp(current.gradientStart, current.gradientEnd, tCurrent);

    if (hasUpper)
    {
        // Normalize height within next biome gradient
        float tNext = saturate((height - next.minSurface) / max(next.maxSurface - next.minSurface, 0.0001));
        float4 nextColor = lerp(next.gradientStart, next.gradientEnd, tNext);

        // Blend between biomes across boundary
        float blendAmount = saturate((height - current.maxSurface) / max(next.minSurface - current.maxSurface, 0.0001));
        return lerp(baseColor, nextColor, blendAmount);
    }

    return baseColor;
}