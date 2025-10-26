#ifndef MC_INCLUDED
#define MC_INCLUDED

#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"

void March(ChunkDispatchKeyInfo key, AppendStructuredBuffer<TriangleData> TriangleBuffer, RWStructuredBuffer<float> DensityMap)
{
    uint cubeIndex = 0;
    float corner[8];
    float3 cornerPos[8];
    
    int step = GetCubeSizeStep(key.chunk.LodIndex);
    float3 world = ToWorld(key.chunk.CoordPos, key.chunk.LodIndex);
    int3 sample3 = GetSamplesPerChunk3();
    
    [loop]
    for (int i = 0; i < 8; i++)
    {
        uint3 pos = key.LocalVoxelCoord + GetCornerOffset(i);
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
    
    /*
    
    ChatGPT helped me with this fuckery. In my previous instance I was making calls to the DensityMap
    itself. Using this below float increased performance by quite a bit, and the lighting looks so much better.
    
    What we're doing is taking the opposite face of the cube and sum them up.
    X - (1-0), (3-2), (5-4), (7-6)
    Y - (2-0), (3-1), (6-4), (7-5)
    Z - (4-0), (5-1), (6-2), (7-3)
    
        y
        |
        4 ---- 5
        /|     /|
        0 ---- 1|
        | 6 --|- 7 ---> x
        |/    |/
        2 ----3
        /
        z

    Approximate central difference from cube corners */
    float3 grad = float3(
        corner[1] - corner[0] + corner[3] - corner[2] + corner[5] - corner[4] + corner[7] - corner[6],
        corner[2] - corner[0] + corner[3] - corner[1] + corner[6] - corner[4] + corner[7] - corner[5],
        corner[4] - corner[0] + corner[5] - corner[1] + corner[6] - corner[2] + corner[7] - corner[3]);
    float3 normal = normalize(grad);

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
        
        // If you like a more minecraft look make RCP its own dedicated variable.
        // I was doing that for quite some time, I liked the look but for some
        // reason bringing them together makes smooth terrain :o
        float t0 = (corner[edge0.x] - ISOLevel) * rcp(corner[edge0.x] - corner[edge0.y]);
        float t1 = (corner[edge1.x] - ISOLevel) * rcp(corner[edge1.x] - corner[edge1.y]);
        float t2 = (corner[edge2.x] - ISOLevel) * rcp(corner[edge2.x] - corner[edge2.y]);
        
        float3 worldA = lerp(cornerPos[edge0.x], cornerPos[edge0.y],t0);
        float3 worldB = lerp(cornerPos[edge1.x], cornerPos[edge1.y],t1);
        float3 worldC = lerp(cornerPos[edge2.x], cornerPos[edge2.y],t2);
        
        TriangleData tri;
        tri.a = worldA;
        tri.b = worldB;
        tri.c = worldC;
        tri.Normal = normal;
        tri.KeyIndex = key.KeyIndex;
        tri.LodIndex = key.chunk.LodIndex;
        
        TriangleBuffer.Append(tri);
    }
}
#endif