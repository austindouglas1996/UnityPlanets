#ifndef BIOMELOOKUP
#define BIOMELOOKUP

#include "../ChunkFunctions.hlsl"

StructuredBuffer<ChunkBiomeData> Biomes;
int _BiomesCount;

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

#endif