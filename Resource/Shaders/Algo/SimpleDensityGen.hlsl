#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "../Lib/PerlinNoise.hlsl"
#include "../ChunkFunctions.hlsl"

int GetOctaves(int lod)
{
    return 6;
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

    // === Base continent layer -> land mask ===
    float3 basePos = (samplePos3D + BaseOffset) * BaseFreq;
    float base01 = N01(fbm3D(basePos, octaves)); 
    float coastLo = SeaLevel - 0.5 * CoastWidth;
    float coastHi = SeaLevel + 0.5 * CoastWidth;
    float landMask = smoothstep(coastLo, coastHi, base01);

    // === Mid/large-scale detail ===
    float3 detailPos = (samplePos3D + DetailOffset) * DetailFreq;
    float detailNoise = fbm3D(detailPos, octaves); // [-1,1] (use N01(...) for outward-only)

    // === Flatness mask ===
    float3 flatPos = (samplePos3D + FlatMaskOffset) * FlatMaskFreq;
    float flatNoise = pow(saturate(1.0 - N01(fbm3D(flatPos, 3))), FlatMaskPower);

    // === Combine ===
    // BaseGain lifts land; detail only on land; (optional) carve oceans a bit
    float rawHeight =
    landMask * BaseGain +
    landMask * (detailNoise * DetailGain) * flatNoise -
    (1.0 - landMask) * OceanDepth;

    return Sanitize(rawHeight * ElevationScale);

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