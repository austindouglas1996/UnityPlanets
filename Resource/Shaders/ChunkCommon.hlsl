#ifndef CHUNK_COMMON_INCLUDED
#define CHUNK_COMMON_INCLUDED

#include "ChunkStruct.hlsl"
#include "ChunkCBuffer.hlsl"

#define SUBVARIANT_LANDMASS 0
#define SUBVARIANT_PLANET   1
#define SUBVARIANT_CAVE     2

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

int GetOctaves(int lod)
{
    switch (lod)
    {
        case 0:
            return 6;
        case 1:
            return 4;
        case 2:
            return 2;
        default:
            return 1;
    }
}

float Sanitize(float v)
{
    return (isnan(v) || isinf(v)) ? 0.0 : v;
}

#endif