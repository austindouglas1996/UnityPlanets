#ifndef BIOMELOOKUP
#define BIOMELOOKUP

#include "WorldNoise.hlsl"

StructuredBuffer<ChunkBiomeData> Biomes;
uint _BiomesCount;

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