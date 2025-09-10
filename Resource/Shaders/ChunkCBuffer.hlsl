#ifndef CHUNK_CBUFFER_STRUCT_INCLUDED
#define CHUNK_CBUFFER_STRUCT_INCLUDED

// ============================================================================
// DensityMapOptions
// Must match C# struct `TerrainDensityOptions` exactly in:
//   - Field order
//   - Data type size/alignment
//   - Purpose
// This is the parameter block used in density generation kernels.
// ============================================================================

cbuffer TerrainDensityOptions
{ 
    // Logical voxel width of a chunk (before LOD).
    int ChunkSize;

    // Noise seed. Use this to derive offsets so worlds are stable per seed.
    int Seed;
    
    // HLSL does not support enum, this is just for the subvarient to help with coding.
    int TerrainType;

    // Iso threshold used by marching. Keep if your meshing kernel reads it.
    float ISOLevel;
   
    // A bias added to temperature to help curve generation.
    int TemperatureBias;

    // A bias added to humidity to help curve generation.
    int HumidityBias;

    // A bias added to foliage to help curve generation.
    int FoliageBias;
    
    // A bias added to help curve generation in sea levels.
    int SeaLevelBias;

    // How wide the continents are. Lower = bigger continents.
    float ContinentFreq;

    // How much the base layer lifts terrain up.
    float ContinentHeight;
    
    // Global height scale for this layer after normalization.
    float ContinentAmp;
    
    // The amount of Octaves to be used for continents.
    uint ContinentOctaves;
    
    // The amount of sea vs land.
    float SeaLevel;
    
    // How wide coasts tend to be.
    float CoastWidth;
    
    // The depth of the ocean.
    float OceanDepth;

    // Size of large-scale bumps on top of the base.
    float DetailFreq;

    // Strength of those broad details.
    float DetailAmp;

    // How large the flat regions run across the map.
    float FlatMaskFreq;

    // Higher -> stronger flattening in masked zones.
    float FlatMaskAmp;
    
    // XYZ offset for the entire generation.
    float3 PositionOffset;

    // XY offset for base landmass noise domain.
    float3 BaseOffset;

    // XY offset for detail domain (replaces +1234).
    float3 DetailOffset;

    // XY offset for flatness domain (replaces +5555).
    float3 FlatMaskOffset;
};

cbuffer PlanetDensityOptions
{
    float3 PlanetCenter;
    float PlanetRadius;
};


float GetSeaLevelBias()
{
    switch (SeaLevelBias)
    {
        case -4:
            return -0.40f; // Confirms there will be no ocean.
        case -2:
            return -0.05f;
        case 0:
            return 0.0f;
        case 2:
            return 0.05f;
        case 4:
            return 0.10f;
        default:
            return 0.0f;
    }
}

float GetHumidityBias()
{
    switch (HumidityBias)
    {
        case -2:
            return -0.05f;
        case 0:
            return 0.0f;
        case 2:
            return 0.15f;
        case 4:
            return 0.30f;
    }
}


#endif