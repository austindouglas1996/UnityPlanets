#ifndef CHUNK_COMMON_INCLUDED
#define CHUNK_COMMON_INCLUDED

#include "ChunkStruct.hlsl"
#include "ChunkCBuffer.hlsl"
#include "MathCommon.hlsl"

#define TYPE_TERRAIN 0
#define TYPE_PLANET  1
#define TYPE_CAVE    2

// Chunk size plus one (account for edge vertices)
int GetChunkLogicalSize()
{
    return ChunkSize + 1;
}

// Logical size as a 3D vector
int3 GetChunkLogicalSize3()
{
    int logicalSize = GetChunkLogicalSize();
    return int3(logicalSize, logicalSize, logicalSize);
}

// Logical size minus one as a 3D vector
int3 GetChunkLogicalSize31()
{
    int logicalSize = GetChunkLogicalSize();
    return int3(logicalSize - 1, logicalSize - 1, logicalSize - 1);
}

// Physical chunk size at a given LOD
int GetChunkSize(int lodIndex)
{
    return ChunkSize << lodIndex;
}

// Step size between points at a given LOD
int GetChunkSizeStep(int lodIndex)
{
    return 1 << lodIndex;
}

// Convert chunk coordinates to world space (LOD0)
float3 ToWorld(int3 coordinates)
{
    return coordinates * GetChunkSize(0);
}

// Convert chunk coordinates to world space (with LOD)
float3 ToWorld(int3 coordinates, int lodIndex)
{
    return coordinates * GetChunkSize(lodIndex);
}

// Convert world position to chunk coordinates
int3 ToCoordinates(float3 worldPos)
{
    int chunkSize = GetChunkSize(0);
    return int3(
        (int) floor(worldPos.x / chunkSize),
        (int) floor(worldPos.y / chunkSize),
        (int) floor(worldPos.z / chunkSize));
}

// Clamp out NaN/Inf to zero
// (This may not actually be needed before, all the instances of this happening was bugs)
float Sanitize(float v)
{
    return (isnan(v) || isinf(v)) ? 0.0 : v;
}

#endif