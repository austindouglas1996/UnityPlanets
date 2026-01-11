#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Includes/Voronoi.hlsl"
#include "Lib/PerlinNoise.hlsl"

/* Step #1 Terrain Class */
struct TerrainInfluence
{
    float deepWater;
    float shallowWater;
    float beach;
    float land;
    float mountain;
    float river;
};

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
    return (uint) (hash1_2D(w.cell.xz) * 3.0) + 2;
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

TerrainInfluence SampleTerrainInfluence(float3 wp)
{
    float2 p = wp.xz + Seed * 100.0;
    
    // Determines if this point is in the ocean, or on land.
    float land = N01(fbm2D(p.x * ContinentFreq, p.y * ContinentFreq, 8));
    
    TerrainInfluence t = (TerrainInfluence) 0;
    
    // Base influence
    t.deepWater = 1.0 - smoothstep(0.45, 0.52, land);
    t.shallowWater = smoothstep(0.48, 0.55, land) * (1.0 - smoothstep(0.55, 0.60, land));
    t.land = smoothstep(0.52, 0.60, land) * (1.0 - smoothstep(0.65, 0.72, land));
    t.mountain = smoothstep(0.65, 0.75, land);
    
    // Water dominance
    float waterMask = saturate(t.deepWater + t.shallowWater);
    t.land *= (1.0 - waterMask);
    t.mountain *= (1.0 - waterMask);
    
    // Mountains dominate land
    t.land *= (1.0 - t.mountain);
    
    // Rivers
    float riverRaw = SampleRiverField(p);
    float riverField = smoothstep(0.35, 0.75, riverRaw);
    float riverMask = t.land * (1.0 - t.deepWater) * (1.0 - t.mountain);
     
    t.river = saturate((riverField - 0.5) * 2.0) * riverMask;
    t.land *= (1.0 - t.river);
    
    // Beaches (derived)
    t.beach = saturate(t.land * t.shallowWater * 2.0);
    
    return t;
}


float GroundHeight(float2 xz, TerrainInfluence t)
{
    float base =
          t.deepWater * 40.0
        + (t.shallowWater + t.river) * 80.0
        + t.land * 120.0;

    // Large-scale land undulation
    float landNoise =
        fbm2D(xz.x * 0.008, xz.y * 0.008, 4);

    base += landNoise * 6.0 * t.land;

    // Small surface detail (mostly land)
    float detail =
        fbm2D(xz.x * 0.03, xz.y * 0.03, 3);

    base += detail * 1.5 * t.land;

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

float SampleHeight(float3 wp)
{
    TerrainInfluence influence = SampleTerrainInfluence(wp);
    
    uint terrainClass = SampleTerrainClass(float3(wp.x, 0, wp.z));

    float ground = GroundHeight(wp.xz, influence);

    if (terrainClass == 5)
    {
        float mountain = MountainHeight(wp.xz);

        // Ensure mountain never dips below ground
        return max(ground, mountain);
    }

    return ground;
}

float ComputeDensity(float3 worldPos)
{
    float height = SampleHeight(worldPos);
    return worldPos.y - height;
}

#endif