#ifndef CHUNK_COMMON_INCLUDED
#define CHUNK_COMMON_INCLUDED

#include "ChunkStruct.hlsl"
#include "ChunkCBuffer.hlsl"
#include "MathCommon.hlsl"

#define TYPE_TERRAIN 0
#define TYPE_PLANET  1
#define TYPE_CAVE    2

// Samples = Cubes + 1
int GetSamplesPerAxis()
{
    return CubesPerAxis + 1;
}

// Vector form of samples (used for buffer allocation / indexing)
int3 GetSamplesPerChunk3()
{
    int sampleSize = GetSamplesPerAxis(); // Core Cubes+1
    int totalSample = sampleSize + (2 * BorderSamplesPerAxis);
    return int3(totalSample, totalSample, totalSample);
}

// Number of cubes per axis at this LOD
int GetCubesPerAxis(int lodIndex)
{
    return CubesPerAxis << lodIndex;
}

// Vector form of cubes per axis
int3 GetCubesPerChunk3(int lodIndex)
{
    int cubes = GetCubesPerAxis(lodIndex);
    return int3(cubes,cubes,cubes);
}

// The step size of each cube based on LOD level.
int GetCubeSizeStep(int lodIndex)
{
    return 1 << lodIndex;
}

// Convert chunk coordinates to world space (with LOD)
float3 ToWorld(int3 coordinates, int lodIndex)
{
    return coordinates * GetCubesPerAxis(lodIndex);
}

// Convert world position to chunk coordinates
int3 ToCoordinates(float3 worldPos)
{
    int CubesPerAxis = GetCubesPerAxis(0);
    return int3(
        (int) floor(worldPos.x / CubesPerAxis),
        (int) floor(worldPos.y / CubesPerAxis),
        (int) floor(worldPos.z / CubesPerAxis));
}

#endif