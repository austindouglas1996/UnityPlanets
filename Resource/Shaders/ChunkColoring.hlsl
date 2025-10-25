#ifndef CHUNK_COMMON_COLORING_INCLUDED
#define CHUNK_COMMON_COLORING_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Biomes/BiomeLookup.hlsl"
#include "Biomes/BiomeSampler.hlsl"
#include "Lib/PerlinNoise.hlsl"

float4 GetColorForDirection(float3 worldPos)
{
    float angle = atan2(worldPos.z, worldPos.x);
    if (angle < 0)
        angle += 2.0 * 3.14159265; // wrap into [0, 2π)
    
    int region = (int) floor(angle / (3.14159265 / 4.0));

    switch (region)
    {
        case 0:
            return float4(1, 0, 0, 1); // East - Red
        case 1:
            return float4(1, 0, 1, 1); // NE - Magenta
        case 2:
            return float4(0, 0, 1, 1); // North - Blue
        case 3:
            return float4(0, 1, 1, 1); // NW - Cyan
        case 4:
            return float4(0, 1, 0, 1); // West - Green
        case 5:
            return float4(0.5, 0.25, 0, 1); // SW - Brownish
        case 6:
            return float4(1, 1, 0, 1); // South - Yellow
        case 7:
            return float4(1, 0.5, 0, 1); // SE - Orange
        default:
            return float4(1, 1, 1, 1); // Fallback white
    }
}

// A special function for returning the LOD color.
float4 GetColorLOD(uint lod)
{
    switch (lod)
    {
        case 0:
            return float4(1.0, 0.0, 0.0, 1.0); // Red
        case 1:
            return float4(1.0, 0.5, 0.0, 1.0); // Orange
        case 2:
            return float4(0.0, 0.0, 1.0, 1.0); // Blue
        case 3:
            return float4(0.0, 1.0, 0.0, 1.0); // Green
        case 4:
            return float4(0.5, 0.8, 1.0, 1.0); // Light blue (snowy)
        case 5:
            return float4(0.6, 0.0, 0.8, 1.0); // Purple
        case 6:
            return float4(1.0, 1.0, 0.0, 1.0); // Yellow
        case 7:
            return float4(0.0, 1.0, 1.0, 1.0); // Cyan
        case 8:
            return float4(1.0, 0.0, 1.0, 1.0); // Magenta
    }


    // Default/fallback
    return float4(1.0, 1.0, 1.0, 1.0);
}

// Retrieves the biome color based on index. Each biome has 6 unique colors.
float4 GetColorByIndex(ChunkBiomeData biome, int idx)
{
    if (idx == 0)
        return biome.Highlight;
    if (idx == 1)
        return biome.Light;
    if (idx == 2)
        return biome.MidLight;
    if (idx == 3)
        return biome.Mid;
    if (idx == 4)
        return biome.Dark;
    return biome.Shadow;
}

// Retrieves the biome blend color based on a biome and world position.
float4 GetBiomeBlend(ChunkBiomeData biome, float3 wp)
{
    // Spatial seed for color variation
    const float ColorFreq = 0.002; // Lower = larger blobs

    // Use XZ world position to sample noise
    float2 p = wp.xz * ColorFreq + Seed;
    float n = N01(fbm3D(float3(p, 0.0), 3)); // N01 = [-1,1] → [0,1]

    // Index into the 6-color palette
    const int N = 6;
    float idxf = n * (N - 1);
    int i0 = (int) floor(idxf);
    int i1 = min(i0 + 1, N - 1);
    float t = frac(idxf);

    // Interpolate between two color bands
    float3 c0 = GetColorByIndex(biome, i0).rgb;
    float3 c1 = GetColorByIndex(biome, i1).rgb;

    return float4(lerp(c0, c1, t), 1.0);
}

float3 GetBiomeBlended(float3 wp)
{
    float scale = ColorSampleRadius;
    
    const float3 offsets[8] =
    {
        float3(scale, 0, 0), float3(-scale, 0, 0),
        float3(0, 0, scale), float3(0, 0, -scale),
        float3(scale, 0, scale), float3(-scale, 0, scale),
        float3(scale, 0, -scale), float3(-scale, 0, -scale)
    };

    float3 accum = 0;
    for (int i = 0; i < 8; i++)
    {
        uint bi = SampleBiomeIndex(wp + offsets[i]);
        accum += GetBiomeBlend(Biomes[bi], wp + offsets[i]).rgb;
    }

    uint biC = SampleBiomeIndex(wp);
    float3 center = GetBiomeBlend(Biomes[biC], wp).rgb;
    accum += center;
    return float3(accum / 9.0);
}

// Retrieves the set color to use for a biome on a vertex.
float4 GetTerrainColor(float3 wp, uint vertex, uint packedBiome)
{
    // Unpack the biome index for this vertex
    uint biomeIndex = UnpackBiomeIndex(packedBiome, vertex);
    ChunkBiomeData biome = Biomes[biomeIndex];
    
    return GetBiomeBlend(biome, wp);
}

// Retrieves the color for a set vertex based on overlay.
float4 GetVertexColor(TriangleData tri, ChunkDetailData data, uint vertex, uint overlay)
{
    float3 wp = vertex == 0 ? tri.a : 
                vertex == 1 ? tri.b : 
                              tri.c;
   
    switch (overlay)
    {
        case 0:
            float3 col = (vertex == 0) ? data.ColorA : (vertex == 1) ? data.ColorB : data.ColorC;
            return float4(col, 1);
        case 1:
            return GetColorLOD(UnpackLOD(data.Biome));
        case 2:
            return ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeHeight, 3);
        case 3:
            return ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeTemperature, 4);
        case 4:
            return ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeHumidty, 3);
        case 5:
            return ReverseQuantizeN(UnpackBiome(data.Biome, vertex).BiomeFoliage, 3);
        case 6:
            return GetColorForDirection(wp);
    }
    
    return float4(255, 0, 238, 1);
}

#endif