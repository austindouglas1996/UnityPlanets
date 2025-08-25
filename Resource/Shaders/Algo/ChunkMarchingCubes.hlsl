#ifndef MC_INCLUDED
#define MC_INCLUDED

#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"
#include "SimpleDensityGen.hlsl"


bool IsEdgeVoxel(int3 voxelCoord)
{
    return voxelCoord.x == ChunkSize - 1 ||
    voxelCoord.y == ChunkSize - 1 ||
    voxelCoord.z == ChunkSize - 1 ||
    voxelCoord.x == 0 ||
    voxelCoord.y == 0 ||
    voxelCoord.z == 0;
}

void ChunkNeighbors(float3 C, out float3 nC[8])
{
    nC[0] = float3(C.x, C.y, C.z + 1); // top (Z+)
    nC[1] = float3(C.x + 1, C.y, C.z); // right (X+)
    nC[2] = float3(C.x, C.y, C.z - 1); // bottom (Z-)
    nC[3] = float3(C.x - 1, C.y, C.z); // left (X-)

    nC[4] = float3(C.x + 1, C.y, C.z + 1); // top-right (X+, Z+)
    nC[5] = float3(C.x + 1, C.y, C.z - 1); // bottom-right (X+, Z-)
    nC[6] = float3(C.x - 1, C.y, C.z - 1); // bottom-left (X-, Z-)
    nC[7] = float3(C.x - 1, C.y, C.z + 1); // top-left (X-, Z+)
}

int GetEdgeSideXZ(int3 voxelCoord)
{
    bool top = voxelCoord.z == ChunkSize;
    bool bottom = voxelCoord.z == 0;
    bool right = voxelCoord.x == ChunkSize;
    bool left = voxelCoord.x == 0;

    if (top && right)
        return 4; // top-right
    if (bottom && right)
        return 5; // bottom-right
    if (bottom && left)
        return 6; // bottom-left
    if (top && left)
        return 7; // top-left

    if (top)
        return 0;
    if (right)
        return 1;
    if (bottom)
        return 2;
    if (left)
        return 3;

    return -1;
}

// Return true only if THIS marching-cubes cell lies on an X/Z side
// whose same-LOD neighbor's WORLD position wants a different LOD.
bool IsEdgeCell(ChunkDispatchKeyInfo key)
{
    int thisLod = GetLODForChunk(key.chunk.CoordPos, key.chunk.LodIndex);
    
    // Build face mask by comparing neighbor desired LOD to *our* desired LOD
    float3 nC[8];
    ChunkNeighbors(key.chunk.CoordPos, nC);
    
    [unroll]
    for (int f = 0; f < 8; ++f)
    {
        int nWant = GetLODForChunk(nC[f], key.chunk.LodIndex);
        
        if (nWant != thisLod && GetEdgeSideXZ(key.voxelCoord) == f)
        {
            return true;
        }
    }
    
    return false;
}






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
        cornerPos[i] = float3(pos) * GetChunkSizeStep(key.chunk.LodIndex) + ToWorld(key.chunk.CoordPos, key.chunk.LodIndex);

        if (corner[i] > ISOLevel)
            cubeIndex |= (1 << i);
    }

    if (cubeIndex == 0 || cubeIndex == 255)
        return;
    
    bool cLod = false;
    if (IsEdgeVoxel(key.voxelCoord) && IsEdgeCell(key))
    {
        cLod = true;
    }

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
        tri.LodKey = GetLODForChunk(key.chunk.CoordPos, key.chunk.LodIndex);
        
        if (cLod)
        {
            tri.LodKey = 10;
        }

        TriangleBuffer.Append(tri);
    }
}

#endif