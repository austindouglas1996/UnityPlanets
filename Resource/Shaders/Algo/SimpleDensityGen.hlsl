#include "../Lib/PerlinNoise.hlsl"
#include "../ChunkFunctions.hlsl"

float GetFoundationNoise(float worldX, float worldY, float worldZ, int lod)
{
    // Decide noise sample coordinates based on variant
    float3 samplePos3D;

    if (SubVariant == SUBVARIANT_PLANET)
    {
        // For planet, use normalized direction from center for consistent wrapping
        float3 wp = float3(worldX, worldY, worldZ);
        float3 dir = normalize(wp - PlanetCenter);

        // Offset so noise moves with BaseOffset/DetailOffset in 3D
        samplePos3D = dir * PlanetRadius;
    }
    else
    {
        // For landmass (and cave for now), just use flat XZ mapping
        samplePos3D = float3(worldX, 0.0, worldZ);
    }

    int octaves = GetOctaves(lod);

    // === Base continent layer ===
    float3 basePos = (samplePos3D + float3(BaseOffset, 0)) * BaseFreq;
    float baseNoise = fbm3D(basePos, 2); // using 3D even for landmass so system is future-proof
    baseNoise = smoothstep(0.0, 1.0, (baseNoise + 1.0) * 0.5);

    // === Mid/large-scale detail ===
    float3 detailPos = (samplePos3D + float3(DetailOffset, 0)) * DetailFreq;
    float detailNoise = fbm3D(detailPos, (lod == 1 ? 6 : octaves));

    // === Flatness mask ===
    float3 flatPos = (samplePos3D + float3(FlatMaskOffset, 0)) * FlatMaskFreq;
    float flatNoise = fbm3D(flatPos, 3);
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

float GetLandMassNoise(float worldX, float worldY, float worldZ, int lod)
{
    float v = GetFoundationNoise(worldX, worldY, worldZ, lod);
    float result = -(worldY - v);
    
    return Sanitize(result);
}

float GetPlanetNoise(float worldX, float worldY, float worldZ, int lod)
{
    float elev = GetFoundationNoise(worldX, worldY, worldZ, lod);
    float dist = length(float3(worldX, worldY, worldZ) - PlanetCenter);
    return (PlanetRadius + elev) - dist; // Positive inside sphere
}

float GenerateNoiseValue(float worldX, float worldY, float worldZ, int lod)
{
    float baseNoise;
    
    if (SubVariant == 0)
    {
        baseNoise = GetLandMassNoise(worldX, worldY, worldZ, lod);

    }
    else if (SubVariant == 1)
    {
        baseNoise = GetPlanetNoise(worldX, worldY, worldZ, lod);
    }
    
    return baseNoise;
}