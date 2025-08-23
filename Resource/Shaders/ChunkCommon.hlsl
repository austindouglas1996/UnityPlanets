#ifndef CHUNK_COMMON_INCLUDED
#define CHUNK_COMMON_INCLUDED

#include "ChunkStruct.hlsl"
#include "ChunkCBuffer.hlsl"

#define SUBVARIANT_LANDMASS 0
#define SUBVARIANT_PLANET   1
#define SUBVARIANT_CAVE     2

int GetChunkSize(ChunkDispatchKey key)
{
    return ChunkSize << key.LodIndex;
}

int GetChunkSizeStep(ChunkDispatchKey key)
{
    return 1 << key.LodIndex;
}

// Coordinates -> world origin (computed on GPU if you didn’t pass WorldPos)
float3 ToWorld(ChunkDispatchKey key)
{
    return key.CoordPos * GetChunkSize(key);
}

// World -> chunk coordinates for this key’s LOD
int3 ToCoordinates(float3 worldPos, ChunkDispatchKey key)
{
    int chunkSize = GetChunkSize(key);
    return int3(
        (int) floor(worldPos.x / chunkSize),
        (int) floor(worldPos.y / chunkSize),
        (int) floor(worldPos.z / chunkSize)
    );
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