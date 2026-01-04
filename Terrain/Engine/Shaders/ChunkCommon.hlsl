#ifndef CHUNK_COMMON_INCLUDED
#define CHUNK_COMMON_INCLUDED

#include "ChunkStruct.hlsl"
#include "ChunkCBuffer.hlsl"
#include "MathCommon.hlsl"

#define TYPE_TERRAIN 0
#define TYPE_PLANET  1
#define TYPE_CAVE    2

//<summary>
//</summary>
uint GetBaseSamplesPerAxis()
{
    return CellsPerAxis + 1;
}

uint GetPaddedSamplesPerAxis()
{
    return GetBaseSamplesPerAxis() + (2 * BorderSamplesPerAxis);
}

uint3 GetBaseSamplesGridSize()
{
    uint cells = GetBaseSamplesPerAxis();
    return uint3(cells, cells, cells);
}

uint3 GetPaddedSamplesGridSize()
{
    uint totalSample = GetPaddedSamplesPerAxis();
    return uint3(totalSample, totalSample, totalSample);
}

int GetChunkCellSpan(uint lodIndex)
{
    return CellsPerAxis << lodIndex;
}

int GetCellStep(uint lodIndex)
{
    return 1 << lodIndex;
}

float3 ChunkOriginToWorld(int3 Origin, uint lodIndex)
{
    return Origin * GetChunkCellSpan(lodIndex);
}

float3 ChunkOriginToWorld(ChunkWorkDescriptor chunk)
{
    return ChunkOriginToWorld(chunk.Origin, chunk.LodIndex);
}

int3 WorldToChunkOrigin(float3 worldPos)
{
    int CellsPerAxis = GetChunkCellSpan(0);
    float inv = rcp((float) CellsPerAxis);

    return int3(
        (int) floor(worldPos.x * inv),
        (int) floor(worldPos.y * inv),
        (int) floor(worldPos.z * inv));
}

#endif