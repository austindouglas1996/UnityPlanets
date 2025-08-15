#ifndef BIOME_SHARED_INCLUDED
#define BIOME_SHARED_INCLUDED

// ============================================================================
// ChunkBiomeData
// Holds the color gradient and height thresholds for a biome.
// Must match the C# struct `BiomeData` exactly in:
//   - Field order
//   - Data type size/alignment
// ============================================================================

struct ChunkBiomeData
{
    // Minimum surface height for this biome (inclusive).
    float minSurface;

    // Maximum surface height for this biome (exclusive).
    float maxSurface;

    // Gradient start color for the biome (usually lower height color).
    float4 gradientStart;

    // Gradient end color for the biome (usually higher height color).
    float4 gradientEnd;
};

// Structured buffer of all active biome definitions.
//   Index 0 = lowest biome, index (_BiomeCount - 1) = highest biome.
StructuredBuffer<ChunkBiomeData> BiomeColors;

// Total number of biomes currently in the buffer.
int _BiomeCount;

#endif 

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
