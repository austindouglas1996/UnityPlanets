#ifndef CHUNK_COMMON_COLORING_INCLUDED
#define CHUNK_COMMON_COLORING_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Biome.hlsl"

static const float4 ColorsArray[8] =
{
    float4(1.0, 0.0, 0.0, 1.0), // Bright red 0
    float4(0.8, 0.0, 0.6, 1.0), // Magenta 1
    float4(0.5, 0.0, 0.8, 1.0), // Deep purple 2
    float4(0.8, 0.3, 0.0, 1.0), // Orange-brown 3
    float4(0.0, 0.7, 0.0, 1.0), // Green 4
    float4(0.6, 0.3, 0.1, 1.0), // Brown 5
    float4(1.0, 0.9, 0.0, 1.0), // Yellow 6
    float4(1.0, 0.4, 0.2, 1.0) // Orange 7
};

float4 GetColorByID(uint id)
{
    return (id < 8) ? ColorsArray[id] : float4(1, 1, 1, 1);
}

float3 VertexColor(uint v)
{
    uint c = v % 3;
    return (c == 0) ? float3(1, 0, 0) :
           (c == 1) ? float3(0, 1, 0) :
                      float3(0, 0, 1);
}

float4 GetVertexColor(TriangleData tri, ChunkDetailData data, uint vertex, uint overlay)
{
    if (tri.LodIndex == 9)
    {
        return float4(0, 0, 0, 1);
    }
    
        if (overlay == 0)
        {
            return float4((vertex == 0) ? data.ColorA :
                      (vertex == 1) ? data.ColorB :
                                      data.ColorC);
        }
  
    
    float3 wp = (vertex == 0) ? tri.a :
                (vertex == 1) ? tri.b :
                                tri.c;

    float4 result = float4(255, 0, 238, 1); // default a pink error color.

    switch (overlay)
    {
        case 1:
            result = ColorsArray[tri.LodIndex];
            break;
        case 2:
            result = ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeHeight, 3);
            break;
        case 3:
            result = ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeTemperature, 4);
            break;
        case 4:
            result = ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeHumidty, 3);
            break;
        case 5:
            result = ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeFoliage, 3);
            break;
        case 7:
            result = float4((vertex == 0) ? float4(tri.NormalA, 1) :
                            (vertex == 1) ? float4(tri.NormalB, 1) :
                                            float4(tri.NormalC, 1));
            break;
        case 8:
            result = float4(VertexColor(vertex), 1);
            break;
    }

    return result;
}


#endif