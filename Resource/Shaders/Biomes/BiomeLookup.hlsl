#ifndef BIOMELOOKUP
#define BIOMELOOKUP

#include "../ChunkFunctions.hlsl"

StructuredBuffer<ChunkBiomeData> Biomes;
int _BiomesCount;

static uint Quantize013(float value)
{
    value = saturate(value);
    if (value < 1.0f / 3.0f)
        return 0;
    if (value < 2.0f / 3.0f)
        return 1;
    return 2;
}

static uint Quantize014(float value)
{
    value = saturate(value);
    if (value < 0.25f)
        return 0;
    if (value < 0.50f)
        return 1;
    if (value < 0.75f)
        return 2;
    return 3;
}

static float ReverseQuantize013(uint value)
{
    if (value == 0)
        return 1.0f / 6.0f; // 0.167
    if (value == 1)
        return 0.5f; // 0.5
    return 5.0f / 6.0f; // 0.833
}

static float ReverseQuantize014(uint value)
{
    if (value == 0)
        return 0.125f;
    if (value == 1)
        return 0.375f;
    if (value == 2)
        return 0.625f;
    return 0.875f;
}

// FindBiomeIndex()
// Takes the set of parameters and determines the first biome that meets the
// qualificiations. If there is a collision using these settings the 4th option,
// foliage will be used to break the kingmaker.
uint FindBiomeIndex(uint height, uint temperature, uint humidity, uint foliage)
{
    uint matches[32]; // assuming max 32 biomes
    uint count = 0;

    // First filter: height + temperature
    for (uint i = 0; i < _BiomesCount; i++)
    {
        ChunkBiomeData b = Biomes[i];
        if (b.BiomeHeight == height && b.BiomeTemperature == temperature)
        {
            matches[count++] = i;
        }
    }

    if (count == 1)
        return matches[0];
    if (count == 0)
        return _BiomesCount - 1;

    // Second filter: humidity
    uint humidMatches[32];
    uint humidCount = 0;
    for (uint i = 0; i < count; i++)
    {
        ChunkBiomeData b = Biomes[matches[i]];
        if (b.BiomeHumidty == humidity)
            humidMatches[humidCount++] = matches[i];
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
uint PackBiomeIndices(uint a, uint b, uint c)
{
    return (a & 0xFF) | ((b & 0xFF) << 8) | ((c & 0xFF) << 16);
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

#endif