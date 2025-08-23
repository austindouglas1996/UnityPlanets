#ifndef MC_INCLUDED
#define MC_INCLUDED

#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"
#include "SimpleDensityGen.hlsl"

void March(
    ChunkDispatchKeyInfo key,
    AppendStructuredBuffer<ChunkTriangleData> TriangleBuffer,
    RWStructuredBuffer<float> DensityMap)
{
    int chunkSize = ChunkSize;
    int chunkLogicalSize = ChunkSize + 1;
    int chunkVoxelSize = chunkLogicalSize * chunkLogicalSize * chunkLogicalSize;
    
    int cubeIndex = 0;
    float corner[8];
    float3 cornerPos[8];

    for (int i = 0; i < 8; i++)
    {
        int3 offset = GetCornerOffset(i);
        int3 pos = key.voxelCoord + offset;

        // Check bounds
        if (any(pos < 0) || pos.x >= chunkLogicalSize || pos.y >= chunkLogicalSize || pos.z >= chunkLogicalSize)
            return;

        int localIndex = pos.x + pos.y * chunkLogicalSize + pos.z * chunkLogicalSize * chunkLogicalSize;
        int fullIndex = key.chunkIndex * chunkVoxelSize + localIndex;
        
        corner[i] = DensityMap[fullIndex];
        cornerPos[i] = float3(pos) * GetChunkSizeStep(key.chunk) + ToWorld(key.chunk);

        if (corner[i] > ISOLevel)
            cubeIndex |= (1 << i);
    }

    if (cubeIndex == 0 || cubeIndex == 255)
        return;

    for (int i = 0; i < 16; i += 3)
    {
        int a = GetTriangleEdgeIndex(cubeIndex, i + 0);
        int b = GetTriangleEdgeIndex(cubeIndex, i + 1);
        int c = GetTriangleEdgeIndex(cubeIndex, i + 2);

        if (a == -1 || b == -1 || c == -1)
            break;

        int2 edge0 = GetEdgeConnection(a);
        int2 edge1 = GetEdgeConnection(b);
        int2 edge2 = GetEdgeConnection(c);

        float3 v0 = lerp(cornerPos[edge0.x], cornerPos[edge0.y],
                         (corner[edge0.x] - ISOLevel) / (corner[edge0.x] - corner[edge0.y] + 0.0001));
        float3 v1 = lerp(cornerPos[edge1.x], cornerPos[edge1.y],
                         (corner[edge1.x] - ISOLevel) / (corner[edge1.x] - corner[edge1.y] + 0.0001));
        float3 v2 = lerp(cornerPos[edge2.x], cornerPos[edge2.y],
                         (corner[edge2.x] - ISOLevel) / (corner[edge2.x] - corner[edge2.y] + 0.0001));

        float3 worldA = v0;
        float3 worldB = v1;
        float3 worldC = v2;

        ChunkTriangleData tri;
        tri.a = worldA;
        tri.b = worldB;
        tri.c = worldC;
        tri.LodKey = key.chunk.LodIndex;

        TriangleBuffer.Append(tri);
    }
}

#endif