#ifndef CHUNK_COMMON_COLORING_INCLUDED
#define CHUNK_COMMON_COLORING_INCLUDED

#include "ChunkCommon.hlsl"

// Structured buffer of all active biome definitions.
StructuredBuffer<ChunkBiomeData> BiomeColors;

// Total number of biomes currently in the buffer.
int _BiomeCount;

// Convert HSV -> RGB (Unity's Color.HSVToRGB equivalent)
float3 HSVtoRGB(float h, float s, float v)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(h + float3(0, K.y, K.z)) * 6.0 - K.w);
    return v * lerp(K.xxx, saturate(p - K.xxx), s);
}

// Get a color based on LOD index (green -> red, like LodColor)
float4 GetLodColor(int lod)
{
    if (lod == 10)
    {
        return float4(1.0, 0.0, 1.0, 1.0); // Bright magenta (R=1, G=0, B=1, A=1)
    }
    
    const int maxLod = 4;

    // t = 0 at farthest (maxLod), 1 at nearest (0)
    float t = saturate((float) (maxLod - lod) / (float) maxLod);

    // Hue: green (0.33) → red (0.0)
    float hue = lerp(0.33, 0.0, t);
    float sat = 0.95;
    float val = 1.0;

    float3 rgb = HSVtoRGB(hue, sat, val);
    return float4(rgb, 1.0); // add alpha = 1
}


float3 GetTerrainColor(float3 normalWS)
{
    float3 rockColor = float3(93, 63, 47) / 255.0; 
    float3 grassColor = float3(61, 102, 46) / 255.0;

    // slope = 1 when flat, 0 when vertical
    float slope = dot(normalize(normalWS), float3(0, 1, 0));

    float grassWeight = saturate(slope * 4.0); 
    float rockWeight = saturate(1.0 - slope * 4.0); 

    return grassColor * grassWeight + rockColor * rockWeight;
}






float GetSurfaceHeightForColor(float3 worldPos)
{
    float height;
    
    if (SubVariant == SUBVARIANT_PLANET)
    {
        float dist = length(worldPos - PlanetCenter);
        height = dist - PlanetRadius; // elevation relative to planet surface
    }
    else
    {
        height = worldPos.y; // flat terrain: just use Y
    }
    
    // Normalize into 0–1 range using ElevationScale
    // So biome MinSurface/MaxSurface can always be defined in [0..1]
    return saturate(height / ElevationScale);
}

// ============================================================================
// GetColorForHeight()
// Returns the interpolated biome color for a given height value.
//
// Logic:
//   1. Find the biome this height belongs to.
//   2. Lerp within the current biome's gradient.
//   3. If the height is near the upper bound, blend into the next biome's
//      gradient for smooth biome transitions.
//
// Parameters:
//   height : The normalized or absolute surface height to sample.
//
// Returns:
//   float4 RGBA color from biome gradient.
// ============================================================================
float4 GetColorForHeight(float height)
{
    // Default to the highest biome if height exceeds all ranges.
    ChunkBiomeData current = BiomeColors[_BiomeCount - 1];
    ChunkBiomeData next = current;
    bool hasUpper = false;

    // Find which biome this height falls into.
    for (int i = 0; i < _BiomeCount - 1; i++)
    {
        if (height >= BiomeColors[i].minSurface && height < BiomeColors[i + 1].minSurface)
        {
            current = BiomeColors[i];

            // Prepare next biome for blending if available.
            if (i + 1 < _BiomeCount)
            {
                next = BiomeColors[i + 1];
                hasUpper = true;
            }
            break;
        }
    }

    // Step 1: Normalize height within the current biome's gradient range.
    float tCurrent = saturate((height - current.minSurface) /
                              max(current.maxSurface - current.minSurface, 0.0001));
    float4 baseColor = lerp(current.gradientStart, current.gradientEnd, tCurrent);

    // Step 2: Blend into the next biome if near the boundary.
    if (hasUpper)
    {
        // Normalize height within the next biome's gradient.
        float tNext = saturate((height - next.minSurface) /
                               max(next.maxSurface - next.minSurface, 0.0001));
        float4 nextColor = lerp(next.gradientStart, next.gradientEnd, tNext);

        // Blend amount between biomes based on proximity to boundary.
        float blendAmount = saturate((height - current.maxSurface) /
                                     max(next.minSurface - current.maxSurface, 0.0001));
        return lerp(baseColor, nextColor, blendAmount);
    }

    return baseColor;
}

#endif