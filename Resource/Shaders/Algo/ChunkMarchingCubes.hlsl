#ifndef MC_INCLUDED
#define MC_INCLUDED

#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"

[noinline]
float3 GetNormal(uint keyIndex, int3 localVoxelCoord, int3 sampleSize, RWStructuredBuffer<float> DensityMap)
{
    float x1 = GetVoxelSampleIndex(localVoxelCoord + int3(BorderSamplesPerAxis, 0, 0), keyIndex, sampleSize);
    float x2 = GetVoxelSampleIndex(localVoxelCoord - int3(BorderSamplesPerAxis, 0, 0), keyIndex, sampleSize);
    
    float y1 = GetVoxelSampleIndex(localVoxelCoord + int3(0, BorderSamplesPerAxis, 0), keyIndex, sampleSize);
    float y2 = GetVoxelSampleIndex(localVoxelCoord - int3(0, BorderSamplesPerAxis, 0), keyIndex, sampleSize);
    
    float z1 = GetVoxelSampleIndex(localVoxelCoord + int3(0, 0, BorderSamplesPerAxis), keyIndex, sampleSize);
    float z2 = GetVoxelSampleIndex(localVoxelCoord - int3(0, 0, BorderSamplesPerAxis), keyIndex, sampleSize);
    
    float dx = DensityMap[x1] - DensityMap[x2];
    float dy = DensityMap[y1] - DensityMap[y2];
    float dz = DensityMap[z1] - DensityMap[z2];
    
    return normalize(float3(dx, dy, dz));
}

void March(ChunkDispatchKeyInfo key, AppendStructuredBuffer<TriangleData> TriangleBuffer, RWStructuredBuffer<float> DensityMap)
{
    int cubeIndex = 0;
    float corner[8];
    float3 cornerPos[8];
    
    int step = GetCubeSizeStep(key.chunk.LodIndex);
    float3 world = ToWorld(key.chunk.CoordPos, key.chunk.LodIndex);
    int3 sample3 = GetSamplesPerChunk3();
    
    [loop]
    for (int i = 0; i < 8; i++)
    {
        int3 pos = key.LocalVoxelCoord + GetCornerOffset(i);
        corner[i] = DensityMap[GetVoxelSampleIndexRaw(pos, key.KeyIndex, sample3)];
        cornerPos[i] = float3(pos) * step + world;
        
        if (corner[i] > ISOLevel)
            cubeIndex |= (1 << i);
    }

    if (cubeIndex == 0 || cubeIndex == 255)
        return;
    
    /*
    ***
    If you want a smooth normal operation it can be done like this. I cannot tell
    if the way I am making normals, or if smooth normals look ugly. So the normal
    operation we use here uses the center origin which makes a flat minecraft look.
    ***
    
    float3 cornerNormal[8];
    
    [loop]
    for (int i = 0; i < 8; i++)
    {
        int3 pos = key.LocalVoxelCoord + GetCornerOffset(i);
        cornerNormal[i] = GetNormal(key, pos, sample3, DensityMap);
    }
    
    you would then normalize the normals like we did with the position
    
    float3 normA = normalize(lerp(cornerNormal[edge0.x], cornerNormal[edge0.y], t0));
    float3 normB = normalize(lerp(cornerNormal[edge1.x], cornerNormal[edge1.y], t1));
    float3 normC = normalize(lerp(cornerNormal[edge2.x], cornerNormal[edge2.y], t2));
    
    If you want even flatter
    float3 normal = normalize(cross(worldB - worldA, worldC - worldA));
    */
    
    // We wait to grab the normal here until after we know there is surface.
    float3 normal = GetNormal(key.KeyIndex, key.LocalVoxelCoord, sample3, DensityMap);
    
    [loop]
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
        
        float t0 = (corner[edge0.x] - ISOLevel) / (corner[edge0.x] - corner[edge0.y]);
        float t1 = (corner[edge1.x] - ISOLevel) / (corner[edge1.x] - corner[edge1.y]);
        float t2 = (corner[edge2.x] - ISOLevel) / (corner[edge2.x] - corner[edge2.y]);
        
        float3 worldA = lerp(cornerPos[edge0.x], cornerPos[edge0.y],t0);
        float3 worldB = lerp(cornerPos[edge1.x], cornerPos[edge1.y],t1);
        float3 worldC = lerp(cornerPos[edge2.x], cornerPos[edge2.y],t2);
        
        TriangleData tri;
        tri.a = worldA;
        tri.b = worldB;
        tri.c = worldC;
        tri.Normal = normal;
        tri.KeyIndex = key.KeyIndex;
        
        TriangleBuffer.Append(tri);
    }
}
#endif