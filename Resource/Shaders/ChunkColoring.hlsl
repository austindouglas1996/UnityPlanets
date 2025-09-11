#ifndef CHUNK_COMMON_COLORING_INCLUDED
#define CHUNK_COMMON_COLORING_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Biomes/BiomeLookup.hlsl"
#include "Lib/PerlinNoise.hlsl"

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
            return float4(0.5, 0.8, 1.0, 1.0); // Light blue (snowy look)
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

// Retrieves the set color to use for a biome on a vertex.
float4 GetTerrainColor(float3 wp, uint vertex, uint packedBiome)
{
    // Unpack the biome index for this vertex
    uint biomeIndex = UnpackBiomeIndex(packedBiome, vertex);
    ChunkBiomeData biome = Biomes[biomeIndex];
    
    return GetBiomeBlend(biome, wp);
}

// Retrieves the color for a set vertex based on overlay.
float4 GetVertexColor(ChunkTriangleData tri, uint vertex, uint overlay)
{
    float3 wp = vertex == 0 ? tri.a : 
                vertex == 1 ? tri.b : 
                              tri.c;
   
    switch (overlay)
    {
        case 0:
            return GetTerrainColor(wp, vertex, tri.Biome);
        case 1:
            return GetColorLOD(UnpackLOD(tri.Biome));
        case 2:
            return ReverseQuantize013(UnpackBiome(tri.Biome, vertex).BiomeHeight);
        case 3:
            return ReverseQuantize014(UnpackBiome(tri.Biome, vertex).BiomeTemperature);
        case 4:
            return ReverseQuantize013(UnpackBiome(tri.Biome, vertex).BiomeHumidty);
        case 5:
            return ReverseQuantize013(UnpackBiome(tri.Biome, vertex).BiomeFoliage);
    }
    
    return float4(255, 0, 238, 1);
}

#endif