#ifndef CHUNK_COMMON_COLORING_INCLUDED
#define CHUNK_COMMON_COLORING_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Biomes/BiomeLookup.hlsl"
#include "Biomes/BiomeSampler.hlsl"
#include "Lib/PerlinNoise.hlsl"

static const float4 ColorsArray[8] =
{
    float4(1, 0, 0, 1),
    float4(0.8, 0, 0.6, 1),
    float4(0.5, 0, 0.8, 1),
    float4(0.8, 0.3, 0, 1),
    float4(0, 0.7, 0, 1),
    float4(0.6, 0.3, 0.1, 1),
    float4(1, 0.9, 0, 1),
    float4(1, 0.4, 0.2, 1)
};

static const float3 ColorScaleOffsets[8] =
{
    float3(ColorSampleRadius, 0, 0), float3(-ColorSampleRadius, 0, 0),
        float3(0, 0, ColorSampleRadius), float3(0, 0, -ColorSampleRadius),
        float3(ColorSampleRadius, 0, ColorSampleRadius), float3(-ColorSampleRadius, 0, ColorSampleRadius),
        float3(ColorSampleRadius, 0, -ColorSampleRadius), float3(-ColorSampleRadius, 0, -ColorSampleRadius)
};

float4 GetColorByID(uint id)
{
    return (id < 8) ? ColorsArray[id] : float4(1, 1, 1, 1);
}

float4 GetColorByIndex(ChunkBiomeData biome, int idx)
{
    float4 cols[6] =
    {
        biome.Highlight,
        biome.Light,
        biome.MidLight,
        biome.Mid,
        biome.Dark,
        biome.Shadow
    };
    return cols[idx];
}

// Retrieves the biome blend color based on a biome and world position.
float4 GetBiomeBlend(ChunkBiomeData biome, float3 wp)
{
    // Spatial seed for color variation
    const float ColorFreq = 0.0002; // Lower = larger blobs

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

float4 GetBiomeBlended(float3 wp)
{
    float3 accum = 0;
    for (int i = 0; i < 8; i++)
    {
        uint bi = SampleBiomeIndex(wp + ColorScaleOffsets[i]);
        accum += GetBiomeBlend(Biomes[bi], wp + ColorScaleOffsets[i]).rgb;
    }

    uint biC = SampleBiomeIndex(wp);
    float3 center = GetBiomeBlend(Biomes[biC], wp).rgb;
    accum += center;
    return float4(accum * INV_9,1);
}

// Retrieves the set color to use for a biome on a vertex.
float4 GetTerrainColor(float3 wp, uint vertex, uint packedBiome)
{
    // Unpack the biome index for this vertex
    uint biomeIndex = UnpackBiomeIndex(packedBiome, vertex);
    ChunkBiomeData biome = Biomes[biomeIndex];
    
    return GetBiomeBlend(biome, wp);
}

float4 GetVertexColor(TriangleData tri, ChunkDetailData data, uint vertex, uint overlay)
{
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
            result = GetColorByID(UnpackLOD(data.Biome));
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
    }

    return result;
}


#endif