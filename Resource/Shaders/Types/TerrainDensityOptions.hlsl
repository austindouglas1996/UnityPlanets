#ifndef DENSITY_OPTIONS_SHARED
#define DENSITY_OPTIONS_SHARED

#define SUBVARIANT_LANDMASS 0
#define SUBVARIANT_PLANET   1
#define SUBVARIANT_CAVE     2

// ============================================================================
// DensityMapOptions
// Must match C# struct `TerrainDensityOptions` exactly in:
//   - Field order
//   - Data type size/alignment
//   - Purpose
// This is the parameter block used in density generation kernels.
// ============================================================================

struct TerrainDensityOptions
{
    // Logical voxel width of a chunk (before LOD).
    int ChunkSize;

    // Noise seed. Use this to derive offsets so worlds are stable per seed.
    int Seed;
    
    // A LOD heat map to see what LOD the chunks are.
    int LODHeatMap;
    
    // HLSL does not support enum, this is just for the subvarient to help with coding.
    int SubVariant;

    // Iso threshold used by marching. Keep if your meshing kernel reads it.
    float ISOLevel;

    // How wide the continents are. Lower = bigger continents.
    float BaseFreq;

    // How much the base layer lifts terrain up.
    float BaseGain;

    // Size of large-scale bumps on top of the base.
    float DetailFreq;

    // Strength of those broad details.
    float DetailGain;

    // How large the flat regions run across the map.
    float FlatMaskFreq;

    // Higher -> stronger flattening in masked zones.
    float FlatMaskPower;

    // Global height scale for this layer after normalization.
    float ElevationScale;

    // XY offset for base landmass noise domain.
    float2 BaseOffset;

    // XY offset for detail domain (replaces +1234).
    float2 DetailOffset;

    // XY offset for flatness domain (replaces +5555).
    float2 FlatMaskOffset;
};

struct PlanetDensityOptions
{
    float3 Center;
    float Radius; 
};

#endif

// Convert HSV -> RGB (Unity's Color.HSVToRGB equivalent)
float3 HSVtoRGB(float h, float s, float v)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(h + float3(0, K.y, K.z)) * 6.0 - K.w);
    return v * lerp(K.xxx, saturate(p - K.xxx), s);
}

// Get a color based on LOD index (green -> red, like LodColor)
float4 GetLodColor(int lod)
{
    const int maxLod = 4;

    // t = 0 at farthest (maxLod), 1 at nearest (0)
    float t = saturate((float) (maxLod - lod) / (float) maxLod);

    // Hue: green (0.33) → red (0.0)
    float hue = lerp(0.33, 0.0, t);
    float sat = 0.95;
    float val = 1.0;

    float3 rgb = HSVtoRGB(hue, sat, val);
    return float4(rgb, 1.0); // add alpha = 1
}

float GetSurfaceHeightForColor(TerrainDensityOptions options, PlanetDensityOptions PlanetOptions, float3 worldPos)
{
    float height;
    
    if (options.SubVariant == SUBVARIANT_PLANET)
    {
        float dist = length(worldPos - PlanetOptions.Center);
        height = dist - PlanetOptions.Radius; // elevation relative to planet surface
    }
    else
    {
        height = worldPos.y; // flat terrain: just use Y
    }
    
    // Normalize into 0–1 range using ElevationScale
    // So biome MinSurface/MaxSurface can always be defined in [0..1]
    return saturate(height / options.ElevationScale);
}
