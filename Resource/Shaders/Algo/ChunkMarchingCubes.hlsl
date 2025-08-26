#ifndef MC_INCLUDED
#define MC_INCLUDED

#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"

void March(
    ChunkDispatchKeyInfo key,
    AppendStructuredBuffer<ChunkTriangleData> TriangleBuffer,
    RWStructuredBuffer<float> DensityMap)
{
    int cubeIndex = 0;
    float corner[8];
    float3 cornerPos[8];
    
    bool cLod = false;
    if (IsEdgeVoxel(key.voxelCoord) && IsEdgeCell(key))
    {
        cLod = false;
    }

    for (int i = 0; i < 8; i++)
    {
        int3 offset = GetCornerOffset(i);
        int3 pos = key.voxelCoord + offset;

        int fullIndex = GetVoxelMapIndex(pos, key.chunkIndex, GetChunkLogicalSize3());

        corner[i] = DensityMap[fullIndex];
        cornerPos[i] = float3(pos) * GetChunkSizeStep(key.chunk.LodIndex) + ToWorld(key.chunk.CoordPos, key.chunk.LodIndex);
        
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
        
        
        float3 worldA = lerp(cornerPos[edge0.x], cornerPos[edge0.y],
                         (corner[edge0.x] - ISOLevel) / (corner[edge0.x] - corner[edge0.y]));
        float3 worldB = lerp(cornerPos[edge1.x], cornerPos[edge1.y],
                         (corner[edge1.x] - ISOLevel) / (corner[edge1.x] - corner[edge1.y]));
        float3 worldC = lerp(cornerPos[edge2.x], cornerPos[edge2.y],
                         (corner[edge2.x] - ISOLevel) / (corner[edge2.x] - corner[edge2.y]));

        ChunkTriangleData tri;
        tri.a = worldA;
        tri.b = worldB;
        tri.c = worldC;
        tri.LodKey = key.chunk.LodIndex;
        
        if (cLod)
        {
            tri.LodKey = 10;
        }

        //TriangleBuffer.Append(tri);
    }
}
#endif