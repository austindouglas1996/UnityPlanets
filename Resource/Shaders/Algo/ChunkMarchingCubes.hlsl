#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"

void March(
    ChunkDispatchKeyInfo key, 
    AppendStructuredBuffer<ChunkTriangleData> TriangleBuffer, 
    StructuredBuffer<float> DensityMap)
{
    int chunkSize = ChunkSize;
    int chunkLogicalSize = ChunkSize + 1;
    int chunkVoxelSize = chunkLogicalSize * chunkLogicalSize * chunkLogicalSize;
    
    int cubeIndex = 0;
    float corner[8];
    float3 cornerPos[8];

    for (int i = 0; i < 8; i++)
    {
        int3 offset = CornerOffsets[i];
        int3 pos = key.voxelCoord + offset;

        // Check bounds
        if (any(pos < 0) || pos.x >= chunkLogicalSize || pos.y >= chunkLogicalSize || pos.z >= chunkLogicalSize)
            return;

        int localIndex = pos.x + pos.y * chunkLogicalSize + pos.z * chunkLogicalSize * chunkLogicalSize;
        int fullIndex = key.chunkIndex * chunkVoxelSize + localIndex;

        corner[i] = DensityMap[fullIndex];
        cornerPos[i] = float3(pos) * (1 << key.chunk.LodIndex);

        if (corner[i] > ISOLevel)
            cubeIndex |= (1 << i);
    }

    if (cubeIndex == 0 || cubeIndex == 255)
        return;

    for (int i = 0; TriangleTable[cubeIndex][i] != -1; i += 3)
    {
        int a = TriangleTable[cubeIndex][i + 0];
        int b = TriangleTable[cubeIndex][i + 1];
        int c = TriangleTable[cubeIndex][i + 2];

        int2 edge0 = EdgeConnections[a];
        int2 edge1 = EdgeConnections[b];
        int2 edge2 = EdgeConnections[c];

        float3 v0 = lerp(cornerPos[edge0.x], cornerPos[edge0.y],
                         (corner[edge0.x] - ISOLevel) / (corner[edge0.x] - corner[edge0.y] + 0.0001));
        float3 v1 = lerp(cornerPos[edge1.x], cornerPos[edge1.y],
                         (corner[edge1.x] - ISOLevel) / (corner[edge1.x] - corner[edge1.y] + 0.0001));
        float3 v2 = lerp(cornerPos[edge2.x], cornerPos[edge2.y],
                         (corner[edge2.x] - ISOLevel) / (corner[edge2.x] - corner[edge2.y] + 0.0001));

        float3 worldA = v0 + ToWorld(key.chunk);
        float3 worldB = v1 + ToWorld(key.chunk);
        float3 worldC = v2 + ToWorld(key.chunk);

        ChunkTriangleData tri;
        
        tri.a = worldA;
        tri.b = worldB;
        tri.c = worldC;
        
        TriangleBuffer.Append(tri);
    }
}