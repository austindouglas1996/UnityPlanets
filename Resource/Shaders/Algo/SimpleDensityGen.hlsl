#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "../Lib/PerlinNoise.hlsl"
#include "../ChunkFunctions.hlsl"

int GetOctaves(int lod)
{
    switch (lod)
    {
        case 0:
            return 6;
        case 1:
            return 4;
        case 2:
            return 2;
        default:
            return 1;
    }
}

float GetFoundationNoise(float3 world, int lod)
{
    // Decide noise sample coordinates based on variant
    float3 samplePos3D;

    if (SubVariant == SUBVARIANT_PLANET)
    {
        // For planet, use normalized direction from center for consistent wrapping
        float3 wp = float3(world.x, world.y, world.z);
        float3 dir = normalize(wp - PlanetCenter);

        // Offset so noise moves with BaseOffset/DetailOffset in 3D
        samplePos3D = dir * PlanetRadius;
    }
    else
    {
        // For landmass (and cave for now), just use flat XZ mapping
        samplePos3D = float3(world.x, 0.0, world.z);
    }

    int octaves = GetOctaves(lod);

    // === Base continent layer ===
    float3 basePos = (samplePos3D + float3(BaseOffset, 0)) * BaseFreq;
    float baseNoise = fbm3D(basePos, 2);
    baseNoise = smoothstep(0.0, 1.0, (baseNoise + 1.0) * 0.5);

    // === Mid/large-scale detail ===
    float3 detailPos = (samplePos3D + float3(DetailOffset, 0)) * DetailFreq;
    float detailNoise = fbm3D(detailPos, octaves);

    // === Flatness mask ===
    float3 flatPos = (samplePos3D + float3(FlatMaskOffset, 0)) * FlatMaskFreq;
    float flatNoise = fbm3D(flatPos, octaves);
    flatNoise = saturate((flatNoise + 1.0) * 0.5);
    flatNoise = pow(saturate(1.0 - flatNoise), FlatMaskPower);

    // === Combine height contributions ===
    float rawHeight =
        baseNoise * BaseGain +
        detailNoise * DetailGain * flatNoise;

    float heightScale = max(BaseGain, 0.0001);
    float normalized = rawHeight / heightScale;
    float finalHeight = normalized * ElevationScale;

    return Sanitize(finalHeight);
}

float GetLandMassNoise(float3 world, int lod)
{
    float v = GetFoundationNoise(world, lod);
    float result = -(world.y - v);
    
    return Sanitize(result);
}

float GetPlanetNoise(float3 world, int lod)
{
    float elev = GetFoundationNoise(world, lod);
    float dist = length(world - PlanetCenter);
    return (PlanetRadius + elev) - dist; // Positive inside sphere
}

float GenerateNoiseValue(float3 chunkCoord, float3 world, int lod)
{
    float baseNoise;
    
    if (SubVariant == 0)
    {
        baseNoise = GetLandMassNoise(world, lod);

    }
    else if (SubVariant == 1)
    {
        baseNoise = GetPlanetNoise(world, lod);
    }
    
    return baseNoise;
}

#endif