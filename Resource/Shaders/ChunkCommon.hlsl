#ifndef CHUNK_COMMON_INCLUDED
#define CHUNK_COMMON_INCLUDED

#include "ChunkStruct.hlsl"
#include "ChunkCBuffer.hlsl"
#include "MathCommon.hlsl"

#define SUBVARIANT_LANDMASS 0
#define SUBVARIANT_PLANET   1
#define SUBVARIANT_CAVE     2

int GetChunkLogicalSize()
{
    return ChunkSize + 1;
}

int3 GetChunkLogicalSize3()
{
    int logicalSize = GetChunkLogicalSize();
    return int3(logicalSize, logicalSize, logicalSize);
}

int3 GetChunkLogicalSize31()
{
    int logicalSize = GetChunkLogicalSize();
    return int3(logicalSize - 1, logicalSize - 1, logicalSize - 1);
}

int GetChunkSize(int lodIndex)
{
    return ChunkSize << lodIndex;
}

int GetChunkSizeStep(int lodIndex)
{
    return 1 << lodIndex;
}

float3 ToWorld(int3 coordinates)
{
    return coordinates * GetChunkSize(0);
}

float3 ToWorld(int3 coordinates, int lodIndex)
{
    return coordinates * GetChunkSize(lodIndex);
}

int3 ToCoordinates(float3 worldPos)
{
    int chunkSize = GetChunkSize(0);
    return int3(
        (int) floor(worldPos.x / chunkSize),
        (int) floor(worldPos.y / chunkSize),
        (int) floor(worldPos.z / chunkSize));
}

float Sanitize(float v)
{
    return (isnan(v) || isinf(v)) ? 0.0 : v;
}

#endif