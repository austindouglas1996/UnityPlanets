#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Includes/Voronoi.hlsl"
#include "Lib/PerlinNoise.hlsl"

/* Step #1 Terrain Class */
int LocalFeatureOffset(float2 p)
{
    float n = N01(fbm2D(p.x * 0.05, p.y * 0.05, 8)); // smooth, continuous

    if (n < 0.33)
        return -1;
    if (n < 0.66)
        return 0;
    return 1;
}

uint SampleLandCellFeature(float2 p)
{
    WorleyResult w = worleyCell(float3(p * 0.003, 0));

    // stable per-region base in [2..4]
    return (uint) (hash1_2D(w.cell) * 3.0) + 2;
}

uint SampleLandClass(float2 p)
{
    uint base = SampleLandCellFeature(p);
    if (base == 21)
        return 21;
    
    int offset = LocalFeatureOffset(p);
    return (uint) clamp(base + offset, 3, 5);
}

float SampleRiverField(float2 p)
{
    WorleyResult w = worleyCell(float3(p * 0.0002, 0));

    // Wide eligibility band around Voronoi edge
    float edgeDist = w.dist2 - w.dist;
    float edgeBand = 1.0 - saturate(edgeDist * 10.0);

    // Meandering noise to choose actual river path
    float riverNoise = fbm2D(p.x * 0.0008, p.y * 0.0025, 4);
    riverNoise = abs(riverNoise);

    // Combine: edge allows, noise decides placement
    float river = edgeBand - riverNoise * 0.6;

    river = saturate(river * 2.0);
    river = pow(river, 2.5);

    return river;
}

uint SampleTerrainClass(float3 worldPos)
{
    float2 p = worldPos.xz + Seed * 100.0;

    float land = N01(fbm2D(p.x * ContinentFreq, p.y * ContinentFreq, 8));

    if (land < 0.50)
        return 0; // deep water

    if (land < 0.56)
        return 1; // shallow water
    
    if (land > 0.67)
        return 5; // mountain
    
    // Rivers cut everything
    float river = SampleRiverField(p);
    if (river > 0.5)
        return 1; // river
    
    if (land < 0.58)
        return 2; // beach

    return SampleLandClass(p);
}


float GroundHeight(float2 xz, uint terrainClass)
{
    float base = 20;

    // Large-scale undulation
    float n = fbm3D(float3(
        xz.x * 0.08,
        0.0,
        xz.y * 0.08
    ), 4);

    base += n * 2.5;

    // Small detail
    float detail = fbm3D(float3(
        xz.x * 0.02,
        100.0,
        xz.y * 0.02
    ), 3);

    base += detail * 1.2;

    return base;
}



float MountainHeight(float2 xz)
{
    float r = length(xz);

    float mountainMask = saturate(1.0 - r / 200.0);
    float height = mountainMask * 300.0;

    float breakup = fbm3D(float3(
        xz.x * 0.01,
        50.0,
        xz.y * 0.01
    ), 4);

    // Fake ridged effect
    breakup = 1.0 - abs(breakup);

    height += breakup * 10.0;

    return height;
}



float SampleHeight(float2 xz)
{
    uint terrainClass = SampleTerrainClass(float3(xz.x, 0, xz.y));

    float ground = GroundHeight(xz, terrainClass);

    if (terrainClass == 5)
    {
        float mountain = MountainHeight(xz);

        // Ensure mountain never dips below ground
        return max(ground, mountain);
    }

    return ground;
}


[noinline]
float GenerateNoiseValue(float3 p)
{
    float height = SampleHeight(p.xz);
    return p.y - height;
}

float ComputeDensity(float3 worldPos)
{
    float height = SampleHeight(worldPos.xz);
    return worldPos.y - height;
}



#endif