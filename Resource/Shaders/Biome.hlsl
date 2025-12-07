#ifndef BIOMELOOKUP
#define BIOMELOOKUP

#include "WorldNoise.hlsl"

StructuredBuffer<ChunkBiomeData> Biomes;
uint _BiomesCount;

static const float3 ColorScaleOffsets[8] =
{
    float3(ColorSampleRadius, 0, 0), float3(-ColorSampleRadius, 0, 0),
        float3(0, 0, ColorSampleRadius), float3(0, 0, -ColorSampleRadius),
        float3(ColorSampleRadius, 0, ColorSampleRadius), float3(-ColorSampleRadius, 0, ColorSampleRadius),
        float3(ColorSampleRadius, 0, -ColorSampleRadius), float3(-ColorSampleRadius, 0, -ColorSampleRadius)
};

// FindBiomeIndex()
// Takes the set of parameters and determines the first biome that meets the
// qualificiations. If there is a collision using these settings the 4th option,
// foliage will be used to break the kingmaker.
uint FindBiomeIndex(uint height, uint temperature, uint humidity, uint foliage)
{
    uint matches[32]; // We are using a max of 32 biomes here.
    uint count = 0;

    // First filter: height + temperature
    for (uint m = 0; m < _BiomesCount; m++)
    {
        ChunkBiomeData b = Biomes[m];
        if (b.BiomeHeight == height && b.BiomeTemperature == temperature)
        {
            matches[count++] = m;
        }
    }

    if (count == 1)
        return matches[0];
    if (count == 0)
        return _BiomesCount - 1;

    // Second filter: humidity
    uint humidMatches[32];
    uint humidCount = 0;
    for (uint h = 0; h < count; h++)
    {
        ChunkBiomeData b = Biomes[matches[h]];
        if (b.BiomeHumidty == humidity)
            humidMatches[humidCount++] = matches[h];
    }

    if (humidCount == 1)
        return humidMatches[0];
    if (humidCount == 0)
        return matches[0]; // fallback to height+temp match

    // Third filter: foliage
    for (uint i = 0; i < humidCount; i++)
    {
        ChunkBiomeData b = Biomes[humidMatches[i]];
        if (b.BiomeFoliage == foliage)
            return humidMatches[i];
    }

    // Still ambiguous? Just pick the first
    return humidMatches[0];
}

// PackBiomeIndices()
// Combines 3 biome indices (one per vertex) into a single uint to save space.
// Used in ChunkTriangleData to reduce memory usage per triangle, which adds up fast at scale.
uint PackBiomeIndices(uint a, uint b, uint c, int lod)
{
    return (a & 0xFF) | ((b & 0xFF) << 8) | ((c & 0xFF) << 16) | ((lod & 0x7) << 24);
}

// UnpackBiomeIndex()
// Pulls a single biome index (0=A, 1=B, 2=C) from the packed uint on a triangle.
// Lets us quickly look up biome info per vertex without needing extra fields.
uint UnpackBiomeIndex(uint packed, int vertex)
{
    return (packed >> (vertex * 8)) & 0xFF;
}

// UnpackBiome()
// A helper method to make it easier to unpack the biome and grab the current biome
// NOTE: This became important on materials because we can't compute the value on a material.
ChunkBiomeData UnpackBiome(uint packed, int vertex)
{
    return Biomes[UnpackBiomeIndex(packed, vertex)];
}

// UnpackLOD
// A fun way to include LOD.
int UnpackLOD(uint packed)
{
    return (int) ((packed >> 24) & 0x7);
}

// SampleBiomeIndex
// Sample a given position to find its biome data which is important for coloring.
uint SampleBiomeIndex(float3 worldPos)
{
    float hVal = SampleHeight(worldPos);
    float tVal = SampleTemperature(worldPos, hVal);
    float mVal = SampleHumidity(worldPos, hVal, tVal);
    float fVal = SampleFoliage(worldPos, hVal, tVal, mVal);

    uint h = QuantizeN(hVal, 3);
    uint t = QuantizeN(tVal, 4);
    uint m = QuantizeN(mVal, 3);
    uint f = QuantizeN(fVal, 3);

    return FindBiomeIndex(h, t, m, f);
}

// SampleBiomeBlend
// Blend a biome within itself generating of its 6 distinct base colors to create a unique feel.
[noinline]
float4 SampleBiomeBlend(ChunkBiomeData biome, float3 wp)
{
    // Use XZ world position to sample noise
    float3 p = wp * 0.005 + Seed * 0.1234;
    float n = worleyWarped(p * 0.4f);
    
    float t = smoothstep(0, 1, 1.0 - n);
    
    float3 final = lerp(biome.MidLight, biome.Dark, t);
    
    return float4(final, 1);

}

[noinline]
float4 SampleBiomeNoiseMap(float3 wp)
{
    // Expand Seed into a usable float3
    float3 p = wp * 0.005 + Seed * 0.1234;

    // Same noise you used in Blend()
    float n = worleyWarped(p * 0.4f);

    // Output as grayscale
    return float4(n, n, n, 1);
}

// SampleBiomeBlended
// Blend a world position with its corresponding three points to find a unique mix of biome blended colors.
[noinline]
float4 SampleBiomeBlended(float3 wp) : NOINLINE
{
    float3 accum = 0;
    
    [loop]
    for (int i = 0; i < 7; i++)
    {
        // We must grab the position of the biome based on our offset
        // we still consider the same world position though as we
        // use a sub noise layer to create round 'blobs' to keep it unique.
        accum += SampleBiomeBlend(Biomes[SampleBiomeIndex(wp + ColorScaleOffsets[i])], wp).rgb;
    }

    return float4(accum * INV_9, 1);
}

#endif